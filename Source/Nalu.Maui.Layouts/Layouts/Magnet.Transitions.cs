using Nalu.MagnetLayout.Engine;

namespace Nalu;

public partial class Magnet
{
    private const string TransitionAnimationName = "Nalu.Magnet.Transition";

    private enum TransitionMode : byte
    {
        Values,
        Frames
    }

    private enum FrameMode : byte
    {
        Interpolate,
        Appear,
        Skip
    }

    private sealed class TransitionState
    {
        public required TransitionMode Mode;
        public required TaskCompletionSource<bool> Completion;
        public double Progress;
        public bool RefreshEnd;

        // Values path
        public double[] StartInputs = [];
        public double[] EndInputs = [];

        // Frames path (indexed by the END tape node index)
        public Rect[] StartFrames = [];
        public Rect[] EndFrames = [];
        public FrameMode[] Modes = [];
        public double[] EndOpacity = [];
        public Size StartMeasured;
        public Size EndMeasured;
        public Size StageSize;

        // Current interpolated frames (kept up to date for retargeting)
        public Rect[] CurrentFrames = [];
        public Size CurrentMeasured;
    }

    /// <summary>
    /// Test hook replacing <see cref="Animation.Commit" />: (layout, animation, length, finished).
    /// </summary>
    internal static Action<Magnet, Animation, uint, Action<double, bool>>? AnimationDriver { get; set; }

    private TransitionState? _transition;
    private double[] _valuesBackup = [];
    private Rect _lastArrangeBounds = new(0, 0, double.NaN, double.NaN);

    internal Rect LastArrangeBounds
    {
        get => _lastArrangeBounds;
        set => _lastArrangeBounds = value;
    }

    /// <summary>
    /// Gets whether a transition is running.
    /// </summary>
    public bool IsTransitioning => _transition is not null;

    /// <summary>
    /// Applies <paramref name="mutate" /> (which edits nodes, toggles view visibility and/or swaps the <see cref="Definition" />)
    /// and animates from the current visual state to the resulting one.
    /// </summary>
    /// <returns><c>true</c> when the transition completed, <c>false</c> when it was interrupted by a newer one.</returns>
    /// <remarks>
    /// Value-only changes (margins, biases, sizes, percents, weights, guideline positions) are animated by interpolating the
    /// constraint inputs, so intermediate states obey the constraint semantics; structural changes interpolate frames.
    /// </remarks>
    public Task<bool> TransitionToAsync(Action mutate, uint length = 250, Easing? easing = null)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        // No visual state yet: just apply.
        if (double.IsNaN(_lastArrangeBounds.Width) || !HasNodes)
        {
            mutate();

            return Task.FromResult(true);
        }

        var padding = Padding;
        var stageSize = new Size(_lastArrangeBounds.Width - padding.HorizontalThickness, _lastArrangeBounds.Height - padding.VerticalThickness);

        // Capture start.
        Dictionary<string, (Rect Frame, bool Visible)> startFrames;
        Size startMeasured;
        double[]? startInputs = null;

        if (_transition is { } running)
        {
            startFrames = CaptureCurrentFrames(running);
            startMeasured = running.CurrentMeasured;

            if (running.Mode == TransitionMode.Values)
            {
                startInputs = new double[_engine.InputCount];
                _engine.CopyInputs(startInputs);
            }

            CancelTransition(running, settle: false);
        }
        else
        {
            EnsureCompiled();

            if (!_engine.HasMeasured)
            {
                _engine.Measure(stageSize.Width, stageSize.Height);
            }

            _engine.Arrange(stageSize.Width, stageSize.Height, false);
            startFrames = CaptureEngineFrames();
            startMeasured = _engine.LastMeasured;
            startInputs = new double[_engine.InputCount];
            _engine.CopyInputs(startInputs);
        }

        // Apply the mutation with notifications suppressed.
        _suppressNotifications = true;
        _suppressedChanges = MagnetChange.None;

        try
        {
            mutate();
        }
        finally
        {
            _suppressNotifications = false;
        }

        var change = _suppressedChanges;
        _dirty |= change;

        var visibilityChanged = VisibilityChanged(startFrames);
        var state = new TransitionState
        {
            Mode = change is MagnetChange.None or MagnetChange.Values && !visibilityChanged && startInputs is not null ? TransitionMode.Values : TransitionMode.Frames,
            Completion = new TaskCompletionSource<bool>(),
            StageSize = stageSize,
            StartMeasured = startMeasured,
            CurrentMeasured = startMeasured
        };

        if (state.Mode == TransitionMode.Values)
        {
            // Keep the start state (including the frozen measured slots), compute the end measured size, restore.
            _engine.SnapshotValues(ref _valuesBackup);
            _engine.PatchValues();
            _dirty = MagnetChange.None;
            state.StartInputs = startInputs!;
            state.EndInputs = new double[_engine.InputCount];
            _engine.CopyInputs(state.EndInputs);
            var args = _engine.LastMeasureArgs;
            state.EndMeasured = _engine.Measure(double.IsNaN(args.Width) ? stageSize.Width : args.Width, double.IsNaN(args.Height) ? stageSize.Height : args.Height);
            _engine.RestoreValues(_valuesBackup);
            _engine.LerpInputs(state.StartInputs, state.EndInputs, 0);
        }
        else
        {
            EnsureCompiled();
            PrepareFrameTransition(state, startFrames);
        }

        _transition = state;

        var animation = new Animation(v => Tick(state, v), 0, 1, easing ?? Easing.CubicInOut);
        Action<double, bool> finished = (_, cancelled) => OnTransitionFinished(state, cancelled);

        if (AnimationDriver is { } driver)
        {
            driver(this, animation, length, finished);
        }
        else
        {
            animation.Commit(this, TransitionAnimationName, 16, length, finished: finished);
        }

#pragma warning disable VSTHRD003
        return state.Completion.Task;
#pragma warning restore VSTHRD003
    }

    /// <summary>
    /// Swaps to a whole new definition, animating nodes matched by <see cref="MagnetNode.MagnetId" />.
    /// </summary>
    public Task<bool> TransitionToAsync(MagnetDefinition end, uint length = 250, Easing? easing = null)
        => TransitionToAsync(() => Definition = end, length, easing);

    private void PrepareFrameTransition(TransitionState state, Dictionary<string, (Rect Frame, bool Visible)> startFrames)
    {
        var stage = state.StageSize;
        var args = _engine.LastMeasureArgs;
        var measureArgs = double.IsNaN(args.Width) ? stage : args;
        var endMeasured = _engine.Measure(measureArgs.Width, measureArgs.Height);

        // Hugging axes follow the new content size, filling axes keep the assigned size.
        var endStageW = Math.Abs(stage.Width - state.StartMeasured.Width) < 0.5 ? endMeasured.Width : stage.Width;
        var endStageH = Math.Abs(stage.Height - state.StartMeasured.Height) < 0.5 ? endMeasured.Height : stage.Height;
        _engine.Arrange(endStageW, endStageH, true);

        var nodes = _engine.Nodes;
        var count = nodes.Length;
        state.EndMeasured = endMeasured;
        state.StartFrames = new Rect[count];
        state.EndFrames = new Rect[count];
        state.CurrentFrames = new Rect[count];
        state.Modes = new FrameMode[count];
        state.EndOpacity = new double[count];

        for (var i = 0; i < count; i++)
        {
            if (nodes[i] is not MagnetView view || view.View is not { } iview)
            {
                state.Modes[i] = FrameMode.Skip;

                continue;
            }

            var endVisible = !_engine.IsCollapsed(i);
            var endFrame = _engine.GetFrame(i);
            state.EndFrames[i] = endFrame;
            var id = view.MagnetId!;

            if (!endVisible)
            {
                state.Modes[i] = FrameMode.Skip;
            }
            else if (startFrames.TryGetValue(id, out var start) && start.Visible)
            {
                state.Modes[i] = FrameMode.Interpolate;
                state.StartFrames[i] = start.Frame;
            }
            else
            {
                state.Modes[i] = FrameMode.Appear;
                state.StartFrames[i] = endFrame;
                state.EndOpacity[i] = iview is VisualElement ve ? ve.Opacity : 1;
            }

            state.CurrentFrames[i] = state.StartFrames[i];
        }
    }

    private Dictionary<string, (Rect Frame, bool Visible)> CaptureEngineFrames()
    {
        var nodes = _engine.Nodes;
        var result = new Dictionary<string, (Rect, bool)>(nodes.Length, StringComparer.Ordinal);

        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] is MagnetView view)
            {
                result[view.MagnetId!] = (_engine.GetFrame(i), !_engine.IsCollapsed(i));
            }
        }

        return result;
    }

    private Dictionary<string, (Rect Frame, bool Visible)> CaptureCurrentFrames(TransitionState running)
    {
        if (running.Mode == TransitionMode.Values)
        {
            return CaptureEngineFrames();
        }

        var nodes = _engine.Nodes;
        var result = new Dictionary<string, (Rect, bool)>(nodes.Length, StringComparer.Ordinal);

        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] is MagnetView view)
            {
                result[view.MagnetId!] = (running.CurrentFrames[i], running.Modes[i] != FrameMode.Skip);
            }
        }

        return result;
    }

    private bool VisibilityChanged(Dictionary<string, (Rect Frame, bool Visible)> startFrames)
    {
        foreach (var node in EffectiveDefinition.AllNodes)
        {
            if (node is not MagnetView view)
            {
                continue;
            }

            var visible = view.View is { } v && v.Visibility != Visibility.Collapsed;

            if (!startFrames.TryGetValue(view.MagnetId!, out var start) || start.Visible != visible)
            {
                return true;
            }
        }

        return false;
    }

    private void Tick(TransitionState state, double t)
    {
        if (!ReferenceEquals(_transition, state))
        {
            return;
        }

        state.Progress = t;
        var padding = Padding;
        var left = _lastArrangeBounds.X + padding.Left;
        var top = _lastArrangeBounds.Y + padding.Top;

        var lerpW = state.StartMeasured.Width + ((state.EndMeasured.Width - state.StartMeasured.Width) * t);
        var lerpH = state.StartMeasured.Height + ((state.EndMeasured.Height - state.StartMeasured.Height) * t);
        state.CurrentMeasured = new Size(lerpW, lerpH);
        var sizeChanges = Math.Abs(state.EndMeasured.Width - state.StartMeasured.Width) > 0.5 || Math.Abs(state.EndMeasured.Height - state.StartMeasured.Height) > 0.5;

        if (state.Mode == TransitionMode.Values)
        {
            if (state.RefreshEnd)
            {
                state.RefreshEnd = false;
                _engine.PatchValues();
                _dirty = MagnetChange.None;
                _engine.CopyInputs(state.EndInputs);
            }

            _engine.LerpInputs(state.StartInputs, state.EndInputs, t);
        }

        if (sizeChanges)
        {
            // Animating the layout's own size: ancestors must reflow (documented as the expensive case).
            InvalidateMeasure();
        }
        else if (state.Mode == TransitionMode.Values)
        {
            _engine.Arrange(state.StageSize.Width, state.StageSize.Height, false);
            MagnetLayoutManager.ArrangeNodes(_engine, left, top);
        }
        else
        {
            ArrangeInterpolatedFrames(state, t, left, top);
        }
    }

    private void ArrangeInterpolatedFrames(TransitionState state, double t, double left, double top)
    {
        var nodes = _engine.Nodes;

        for (var i = 0; i < nodes.Length; i++)
        {
            if (state.Modes[i] == FrameMode.Skip || _engine.GetView(i) is not { } view)
            {
                continue;
            }

            var s = state.StartFrames[i];
            var e = state.EndFrames[i];
            var frame = new Rect(
                s.X + ((e.X - s.X) * t),
                s.Y + ((e.Y - s.Y) * t),
                s.Width + ((e.Width - s.Width) * t),
                s.Height + ((e.Height - s.Height) * t)
            );
            state.CurrentFrames[i] = frame;

            if (state.Modes[i] == FrameMode.Appear && view is VisualElement ve)
            {
                ve.Opacity = state.EndOpacity[i] * t;
            }

            view.Arrange(frame.Offset(left, top));
        }
    }

    internal Size TransitionMeasure()
    {
        var padding = Padding;
        var state = _transition!;

        return new Size(state.CurrentMeasured.Width + padding.HorizontalThickness, state.CurrentMeasured.Height + padding.VerticalThickness);
    }

    internal void TransitionArrange(Rect bounds)
    {
        var state = _transition!;
        var padding = Padding;
        var left = bounds.X + padding.Left;
        var top = bounds.Y + padding.Top;

        if (state.Mode == TransitionMode.Values)
        {
            _engine.Arrange(bounds.Width - padding.HorizontalThickness, bounds.Height - padding.VerticalThickness, false);
            MagnetLayoutManager.ArrangeNodes(_engine, left, top);
        }
        else
        {
            ArrangeInterpolatedFrames(state, state.Progress, left, top);
        }
    }

    private void OnTransitionFinished(TransitionState state, bool cancelled)
    {
        if (!ReferenceEquals(_transition, state))
        {
            // Already cancelled/retargeted.
            return;
        }

        _transition = null;

        if (!cancelled)
        {
            if (state.Mode == TransitionMode.Values)
            {
                _engine.LerpInputs(state.StartInputs, state.EndInputs, 1);
            }
            else
            {
                RestoreOpacity(state);
            }
        }

        // Settle through the normal pipeline.
        InvalidateMeasure();
        state.Completion.TrySetResult(!cancelled);
    }

    private void CancelTransition(TransitionState state, bool settle)
    {
        if (!ReferenceEquals(_transition, state))
        {
            return;
        }

        _transition = null;
        this.AbortAnimation(TransitionAnimationName);

        if (state.Mode == TransitionMode.Values)
        {
            _engine.LerpInputs(state.StartInputs, state.EndInputs, 1);
        }
        else
        {
            RestoreOpacity(state);
        }

        if (settle)
        {
            InvalidateMeasure();
        }

        state.Completion.TrySetResult(false);
    }

    private void RestoreOpacity(TransitionState state)
    {
        for (var i = 0; i < state.Modes.Length; i++)
        {
            if (state.Modes[i] == FrameMode.Appear && _engine.GetView(i) is VisualElement ve)
            {
                ve.Opacity = state.EndOpacity[i];
            }
        }
    }

    /// <summary>
    /// Called by the owner notification path while a transition is running (external changes).
    /// </summary>
    private void OnExternalChangeDuringTransition(MagnetChange change)
    {
        var state = _transition!;

        if (state.Mode == TransitionMode.Values && change == MagnetChange.Values)
        {
            _dirty |= change;
            state.RefreshEnd = true;

            return;
        }

        _dirty |= change;
        CancelTransition(state, settle: true);
    }
}
