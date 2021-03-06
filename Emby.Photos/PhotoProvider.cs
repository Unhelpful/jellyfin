using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using TagLib;
using TagLib.IFD;
using TagLib.IFD.Entries;
using TagLib.IFD.Tags;

namespace Emby.Photos
{
    public class PhotoProvider : ICustomMetadataProvider<Photo>, IForcedProvider, IHasItemChangeMonitor
    {
        private readonly ILogger _logger;
        private readonly IImageProcessor _imageProcessor;

        // These are causing taglib to hang
        private string[] _includextensions = new string[] { ".jpg", ".jpeg", ".png", ".tiff", ".cr2" };

        public PhotoProvider(ILogger logger, IImageProcessor imageProcessor)
        {
            _logger = logger;
            _imageProcessor = imageProcessor;
        }

        public string Name => "Embedded Information";

        public bool HasChanged(BaseItem item, IDirectoryService directoryService)
        {
            if (item.IsFileProtocol)
            {
                var file = directoryService.GetFile(item.Path);
                return (file != null && file.LastWriteTimeUtc != item.DateModified);
            }

            return false;
        }

        public Task<ItemUpdateType> FetchAsync(Photo item, MetadataRefreshOptions options, CancellationToken cancellationToken)
        {
            item.SetImagePath(ImageType.Primary, item.Path);

            // Examples: https://github.com/mono/taglib-sharp/blob/a5f6949a53d09ce63ee7495580d6802921a21f14/tests/fixtures/TagLib.Tests.Images/NullOrientationTest.cs
            if (_includextensions.Contains(Path.GetExtension(item.Path), StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using (var file = TagLib.File.Create(item.Path))
                    {
                        if (file.GetTag(TagTypes.TiffIFD) is IFDTag tag)
                        {
                            var structure = tag.Structure;
                            if (structure != null
                                && structure.GetEntry(0, (ushort)IFDEntryTag.ExifIFD) is SubIFDEntry exif)
                            {
                                var exifStructure = exif.Structure;
                                if (exifStructure != null)
                                {
                                    var entry = exifStructure.GetEntry(0, (ushort)ExifEntryTag.ApertureValue) as RationalIFDEntry;
                                    if (entry != null)
                                    {
                                        item.Aperture = (double)entry.Value.Numerator / entry.Value.Denominator;
                                    }

                                    entry = exifStructure.GetEntry(0, (ushort)ExifEntryTag.ShutterSpeedValue) as RationalIFDEntry;
                                    if (entry != null)
                                    {
                                        item.ShutterSpeed = (double)entry.Value.Numerator / entry.Value.Denominator;
                                    }
                                }
                            }
                        }

                        if (file is TagLib.Image.File image)
                        {
                            item.CameraMake = image.ImageTag.Make;
                            item.CameraModel = image.ImageTag.Model;

                            item.Width = image.Properties.PhotoWidth;
                            item.Height = image.Properties.PhotoHeight;

                            var rating = image.ImageTag.Rating;
                            if (rating.HasValue)
                            {
                                item.CommunityRating = rating;
                            }
                            else
                            {
                                item.CommunityRating = null;
                            }

                            item.Overview = image.ImageTag.Comment;

                            if (!string.IsNullOrWhiteSpace(image.ImageTag.Title)
                                && !item.LockedFields.Contains(MetadataFields.Name))
                            {
                                item.Name = image.ImageTag.Title;
                            }

                            var dateTaken = image.ImageTag.DateTime;
                            if (dateTaken.HasValue)
                            {
                                item.DateCreated = dateTaken.Value;
                                item.PremiereDate = dateTaken.Value;
                                item.ProductionYear = dateTaken.Value.Year;
                            }

                            item.Genres = image.ImageTag.Genres;
                            item.Tags = image.ImageTag.Keywords;
                            item.Software = image.ImageTag.Software;

                            if (image.ImageTag.Orientation == TagLib.Image.ImageOrientation.None)
                            {
                                item.Orientation = null;
                            }
                            else if (Enum.TryParse(image.ImageTag.Orientation.ToString(), true, out ImageOrientation orientation))
                            {
                                item.Orientation = orientation;
                            }

                            item.ExposureTime = image.ImageTag.ExposureTime;
                            item.FocalLength = image.ImageTag.FocalLength;

                            item.Latitude = image.ImageTag.Latitude;
                            item.Longitude = image.ImageTag.Longitude;
                            item.Altitude = image.ImageTag.Altitude;

                            if (image.ImageTag.ISOSpeedRatings.HasValue)
                            {
                                item.IsoSpeedRating = Convert.ToInt32(image.ImageTag.ISOSpeedRatings.Value);
                            }
                            else
                            {
                                item.IsoSpeedRating = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Image Provider - Error reading image tag for {0}", item.Path);
                }
            }

            if (item.Width <= 0 || item.Height <= 0)
            {
                var img = item.GetImageInfo(ImageType.Primary, 0);

                try
                {
                    var size = _imageProcessor.GetImageDimensions(item, img, false);

                    if (size.Width > 0 && size.Height > 0)
                    {
                        item.Width = size.Width;
                        item.Height = size.Height;
                    }
                }
                catch (ArgumentException)
                {
                    // format not supported
                }
            }

            const ItemUpdateType Result = ItemUpdateType.ImageUpdate | ItemUpdateType.MetadataImport;
            return Task.FromResult(Result);
        }
    }
}
