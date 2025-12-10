using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using AView = Android.Views.View;

namespace Nalu;

#pragma warning disable CS1591
public class NaluShellItemRendererNavigationBlurSupportLayout : NaluShellItemRendererNavigationLayout
{
    public static readonly bool IsSupported = OperatingSystem.IsAndroidVersionAtLeast(31);
    
#pragma warning disable CA1416
    private readonly RenderNode _contentRenderNode = new("NaluShellItemRendererNavigationLayoutContent");
    private readonly RenderNode _blurRenderNode = new("NaluShellItemRendererNavigationLayoutBlur");
#pragma warning restore CA1416
    private RenderEffect? _blurEffect;
    private RenderEffect? _shaderEffect;
    private RenderEffect? _combinedEffect;
    private Shader? _shader;

    private AView TabBarLayout => ((ViewGroup) Parent!).GetChildAt(1)!;
    
    public NaluShellItemRendererNavigationBlurSupportLayout(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public NaluShellItemRendererNavigationBlurSupportLayout(Context context) : base(context)
    {
    }

    protected override void DispatchDraw(Canvas canvas)
    {
        if (canvas.IsHardwareAccelerated)
        {
#pragma warning disable CA1416
            var canvasWidth = canvas.Width;
            var canvasHeight = canvas.Height;
            _contentRenderNode.SetPosition(0, 0, canvasWidth, canvasHeight);
            using var contentCanvas = _contentRenderNode.BeginRecording();
            base.DispatchDraw(contentCanvas);
            _contentRenderNode.EndRecording();

            canvas.DrawRenderNode(_contentRenderNode);

            var tabBarLayout = TabBarLayout;
            
            // Commented out for now
            var tabBarHeight = tabBarLayout.Visibility == ViewStates.Gone ? 0 : tabBarLayout.Height;
            
            if (tabBarHeight > 0)
            {
                DisposeResources();

                _blurRenderNode.SetPosition(0, 0, canvasWidth, tabBarHeight);
                _blurRenderNode.SetTranslationY(canvasHeight - tabBarHeight);
                
                _blurEffect = NaluTabBar.BlurEffectFactory(Context!);
                _shader = NaluTabBar.BlurShaderFactory(canvasWidth, tabBarHeight);

                if (_shader is null)
                {
                    _blurRenderNode.SetRenderEffect(_blurEffect);
                }
                else
                {
                    // Create a color filter effect that uses the gradient as a mask
                    _shaderEffect = RenderEffect.CreateShaderEffect(_shader);
                    _combinedEffect = RenderEffect.CreateBlendModeEffect(_shaderEffect, _blurEffect, Android.Graphics.BlendMode.SrcIn!);
                    _blurRenderNode.SetRenderEffect(_combinedEffect);
                }

                using var blurCanvas = _blurRenderNode.BeginRecording();
                blurCanvas.Translate(0f, -(canvasHeight - tabBarHeight));
                blurCanvas.DrawRenderNode(_contentRenderNode);
                _blurRenderNode.EndRecording();
                
                canvas.DrawRenderNode(_blurRenderNode);
            }
#pragma warning restore CA1416
        }
        else
        {
            base.DispatchDraw(canvas);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            DisposeResources();
            _blurRenderNode.Dispose();
            _contentRenderNode.Dispose();
        }
    }

    private void DisposeResources()
    {
        _shader?.Dispose();
        _blurEffect?.Dispose();
        _shaderEffect?.Dispose();
        _combinedEffect?.Dispose();
                
        _shader = null;
        _blurEffect = null;
        _shaderEffect = null;
        _combinedEffect = null;
    }
}
