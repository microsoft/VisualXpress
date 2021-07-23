// Copyright Microsoft Corp. All Rights Reserved.
using System;

namespace Microsoft.VisualXpress
{
    static class GuidList
    {
        public const string GuidVisualXpressPkgString = "87cfaa4e-e594-4b22-a745-f8dd9be5038b";
		public const string GuidVisualXpressCmdSetString = "52400462-6dad-47d9-9433-b59f1f0ec889";
		public const string GuidVisualXpressOutputPaneString = "80060c96-240c-4b45-ab2e-a37dd64460f6";
		public const string GuidVisualXpressExtensionGalleryString = "006509f3-e12a-4945-a86b-a3fe0746c597";

		public static readonly Guid GuidVisualXpressPkg = new Guid(GuidVisualXpressPkgString);
		public static readonly Guid GuidVisualXpressCmdSet = new Guid(GuidVisualXpressCmdSetString);
		public static readonly Guid GuidVisualXpressOutputPane = new Guid(GuidVisualXpressOutputPaneString);
		public static readonly Guid GuidVisualXpressExtensionGallery = new Guid(GuidVisualXpressExtensionGalleryString);
    };
}