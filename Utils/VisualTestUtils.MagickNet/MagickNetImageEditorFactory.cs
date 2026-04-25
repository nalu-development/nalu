// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See THIRD-PARTY-NOTICES.md for full license information.

namespace VisualTestUtils.MagickNet;

public class MagickNetImageEditorFactory : IImageEditorFactory
{
    public IImageEditor CreateImageEditor(ImageSnapshot imageSnapshot) =>
        new MagickNetImageEditor(imageSnapshot);
}