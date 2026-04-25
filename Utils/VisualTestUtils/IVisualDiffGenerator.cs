// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See THIRD-PARTY-NOTICES.md for full license information.

namespace VisualTestUtils;

/// <summary>
/// Interface for image verification.
/// </summary>
public interface IVisualDiffGenerator
{
    /// <summary>
    /// Create a diff image, highlighting the differences between the two snapshots.
    /// </summary>
    /// <param name="baselineImage">Baseline image.</param>
    /// <param name="actualImage">Actual image.</param>
    /// <returns>Image highlighting differences in red.</returns>
    ImageSnapshot GenerateDiff(ImageSnapshot baselineImage, ImageSnapshot actualImage);
}