// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See THIRD-PARTY-NOTICES.md for full license information.

namespace VisualTestUtils;

public class ImagePercentageDifference : ImageDifference
{
    private readonly double _percentage;

    public ImagePercentageDifference(double percentage)
    {
        _percentage = percentage;
    }

    public override string Description =>
        $"{_percentage * 100.0:0.00}% difference";
}
