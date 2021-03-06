﻿// <copyright file="IAssetExtensionsFixture.cs" company="Microsoft">Copyright 2013 Microsoft Corporation</copyright>
// <license>
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </license>

namespace MediaServices.Client.Extensions.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.MediaServices.Client;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;

    [TestClass]
    public class IAssetExtensionsFixture
    {
        private static readonly int[] Widths = { 240, 480, 480, 720, 720, 960 };
        private static readonly int[] Heights = { 180, 360, 360, 540, 540, 720 };

        private CloudMediaContext context;
        private IAsset asset;
        private IAsset outputAsset;

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowGetAssetMetadataIfAssetIsNull()
        {
            try
            {
                IAsset nullAsset = null;

                nullAsset.GetMetadata();
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw exception.InnerException;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowGetAssetMetadataWithSpecificLocatorIfAssetIsNull()
        {
            try
            {
                IAsset nullAsset = null;

                var sasLocator = this.context.Locators.Where(l => l.Type == LocatorType.Sas).FirstOrDefault();

                nullAsset.GetMetadata(sasLocator);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw exception.InnerException;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldThrowGetAssetMetadataIfLocatorIsNull()
        {
            try
            {
                this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

                this.asset.GetMetadata(null);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw exception.InnerException;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldThrowGetAssetMetadataIfLocatorTypeIsNotSas()
        {
            try
            {
                this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

                var originLocator = this.context.Locators.Create(LocatorType.OnDemandOrigin, this.asset, AccessPermissions.Read, TimeSpan.FromDays(1));

                this.asset.GetMetadata(originLocator);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
                throw exception.InnerException;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldThrowGetAssetMetadataIfLocatorDoesNotBelongToParentAsset()
        {
            try
            {
                var asset2 = this.context.Assets.Create("empty", AssetCreationOptions.None);
                var sasLocator = this.context.Locators.Create(LocatorType.Sas, asset2, AccessPermissions.Read, TimeSpan.FromDays(1));

                this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

                try
                {
                    this.asset.GetMetadata(sasLocator);
                }
                finally
                {
                    if (asset2 != null)
                    {
                        asset2.Delete();
                    }
                }
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
                throw exception.InnerException;
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldReturnNullGetAssetMetadataIfAssetDoesNotContainMetadataFile()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

            var assetMetadata = this.asset.GetMetadata();

            Assert.IsNull(assetMetadata);
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv")]
        public void ShouldGetAssetMetadata()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\smallwmv1.wmv", AssetCreationOptions.None);

            var job = this.context.Jobs.CreateWithSingleTask(
                MediaProcessorNames.MediaEncoderStandard,
                MediaEncoderStandardTaskPresetStrings.H264MultipleBitrate720p,
                this.asset,
                "Output Asset Name",
                AssetCreationOptions.None);

            job.Submit();
            job.GetExecutionProgressTask(CancellationToken.None).Wait();

            this.outputAsset = job.OutputMediaAssets[0];

            var assetMetadata = this.outputAsset.GetMetadata();

            Assert.AreEqual(0, this.outputAsset.Locators.Count());

            var assetFilesArray = this.outputAsset
                .AssetFiles
                .ToArray()
                .Where(af => !af.Name.EndsWith(IAssetExtensions.MetadataFileSuffix, StringComparison.OrdinalIgnoreCase)
                    && !af.Name.EndsWith(ILocatorExtensions.ManifestFileExtension, StringComparison.OrdinalIgnoreCase)
                    && !af.Name.EndsWith(ILocatorExtensions.ClientManifestFileExtension, StringComparison.OrdinalIgnoreCase)
                    && !af.Name.EndsWith(IAssetExtensions.InputMetadataFileSuffix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(af => af.Name)
                .ToArray();

            var assetMetadataArray = assetMetadata.OrderBy(am => am.Name).ToArray();

            Assert.AreEqual(assetFilesArray.Length, assetMetadataArray.Length);

            for (int i = 0; i < assetFilesArray.Length; i++)
            {
                Assert.AreEqual(assetFilesArray[i].Name, assetMetadataArray[i].Name);
                Assert.AreEqual(assetFilesArray[i].ContentFileSize, assetMetadataArray[i].Size);
                Assert.AreEqual(TimeSpan.FromSeconds(5.206), assetMetadataArray[i].Duration);

                Assert.IsNotNull(assetMetadataArray[i].Sources);
                Assert.AreEqual(1, assetMetadataArray[i].Sources.Count());
                Assert.IsNotNull(assetMetadataArray[i].Sources.ElementAt(0));
                Assert.AreEqual(assetFilesArray[i].Name, assetMetadataArray[i].Sources.ElementAt(0).Name);

                Assert.IsNotNull(assetMetadataArray[i].VideoTracks);
                Assert.AreEqual(1, assetMetadataArray[i].VideoTracks.Count());
                Assert.IsNotNull(assetMetadataArray[i].VideoTracks.ElementAt(0));
                Assert.AreEqual(1, assetMetadataArray[i].VideoTracks.ElementAt(0).Id);
                Assert.AreEqual("avc1", assetMetadataArray[i].VideoTracks.ElementAt(0).FourCC, true);
                Assert.AreEqual(Widths[i], assetMetadataArray[i].VideoTracks.ElementAt(0).Width);
                Assert.AreEqual(Heights[i], assetMetadataArray[i].VideoTracks.ElementAt(0).Height);
                Assert.AreEqual(4, assetMetadataArray[i].VideoTracks.ElementAt(0).DisplayAspectRatioNumerator);
                Assert.AreEqual(3, assetMetadataArray[i].VideoTracks.ElementAt(0).DisplayAspectRatioDenominator);
                ////Assert.AreEqual(29.971, assetMetadataArray[i].VideoTracks.ElementAt(0).TargetFramerate);

                Assert.IsNotNull(assetMetadataArray[i].AudioTracks);
                Assert.AreEqual(1, assetMetadataArray[i].AudioTracks.Count());
                Assert.IsNotNull(assetMetadataArray[i].AudioTracks.ElementAt(0));
                ////Assert.IsNull(assetMetadataArray[i].AudioTracks.ElementAt(0).EncoderVersion);
                Assert.AreEqual(2, assetMetadataArray[i].AudioTracks.ElementAt(0).Id);
                Assert.AreEqual("aac", assetMetadataArray[i].AudioTracks.ElementAt(0).Codec, true);
                Assert.AreEqual(2, assetMetadataArray[i].AudioTracks.ElementAt(0).Channels);
                Assert.AreEqual(48000, assetMetadataArray[i].AudioTracks.ElementAt(0).SamplingRate);
                Assert.AreEqual(128, assetMetadataArray[i].AudioTracks.ElementAt(0).Bitrate);
                ////Assert.AreEqual(0, assetMetadataArray[i].AudioTracks.ElementAt(0).BitsPerSample);
                Assert.AreEqual("und", assetMetadataArray[i].AudioTracks.ElementAt(0).Language, true);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv")]
        public void ShouldGetAssetMetadataWithSpecificLocator()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\smallwmv1.wmv", AssetCreationOptions.None);

            var job = this.context.Jobs.CreateWithSingleTask(
                MediaProcessorNames.MediaEncoderStandard,
                MediaEncoderStandardTaskPresetStrings.H264MultipleBitrate720p,
                this.asset,
                "Output Asset Name",
                AssetCreationOptions.None);

            job.Submit();
            job.GetExecutionProgressTask(CancellationToken.None).Wait();

            this.outputAsset = job.OutputMediaAssets[0];

            var sasLocator = this.context.Locators.Create(LocatorType.Sas, this.outputAsset, AccessPermissions.Read, TimeSpan.FromDays(1));

            var assetMetadata = this.outputAsset.GetMetadata(sasLocator);

            var assetFilesArray = this.outputAsset
                .AssetFiles
                .ToArray()
                .Where(af => !af.Name.EndsWith(IAssetExtensions.MetadataFileSuffix, StringComparison.OrdinalIgnoreCase)
                    && !af.Name.EndsWith(ILocatorExtensions.ManifestFileExtension, StringComparison.OrdinalIgnoreCase)
                    && !af.Name.EndsWith(ILocatorExtensions.ClientManifestFileExtension, StringComparison.OrdinalIgnoreCase)
                    && !af.Name.EndsWith(IAssetExtensions.InputMetadataFileSuffix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(af => af.Name)
                .ToArray();
            var assetMetadataArray = assetMetadata.OrderBy(am => am.Name).ToArray();

            Assert.AreEqual(assetFilesArray.Length, assetMetadataArray.Length);

            for (int i = 0; i < assetFilesArray.Length; i++)
            {
                Assert.AreEqual(assetFilesArray[i].Name, assetMetadataArray[i].Name);
                Assert.AreEqual(assetFilesArray[i].ContentFileSize, assetMetadataArray[i].Size);
                Assert.AreEqual(TimeSpan.FromSeconds(5.206), assetMetadataArray[i].Duration);

                Assert.IsNotNull(assetMetadataArray[i].Sources);
                Assert.AreEqual(1, assetMetadataArray[i].Sources.Count());
                Assert.IsNotNull(assetMetadataArray[i].Sources.ElementAt(0));
                Assert.AreEqual(assetFilesArray[i].Name, assetMetadataArray[i].Sources.ElementAt(0).Name);

                Assert.IsNotNull(assetMetadataArray[i].VideoTracks);
                Assert.AreEqual(1, assetMetadataArray[i].VideoTracks.Count());
                Assert.IsNotNull(assetMetadataArray[i].VideoTracks.ElementAt(0));
                Assert.AreEqual(1, assetMetadataArray[i].VideoTracks.ElementAt(0).Id);
                Assert.AreEqual("avc1", assetMetadataArray[i].VideoTracks.ElementAt(0).FourCC, true);
                Assert.AreEqual(Widths[i], assetMetadataArray[i].VideoTracks.ElementAt(0).Width);
                Assert.AreEqual(Heights[i], assetMetadataArray[i].VideoTracks.ElementAt(0).Height);
                Assert.AreEqual(4, assetMetadataArray[i].VideoTracks.ElementAt(0).DisplayAspectRatioNumerator);
                Assert.AreEqual(3, assetMetadataArray[i].VideoTracks.ElementAt(0).DisplayAspectRatioDenominator);
                ////Assert.AreEqual(29.971, assetMetadataArray[i].VideoTracks.ElementAt(0).TargetFramerate);

                Assert.IsNotNull(assetMetadataArray[i].AudioTracks);
                Assert.AreEqual(1, assetMetadataArray[i].AudioTracks.Count());
                Assert.IsNotNull(assetMetadataArray[i].AudioTracks.ElementAt(0));
                ////Assert.IsNull(assetMetadataArray[i].AudioTracks.ElementAt(0).EncoderVersion);
                Assert.AreEqual(2, assetMetadataArray[i].AudioTracks.ElementAt(0).Id);
                Assert.AreEqual("aac", assetMetadataArray[i].AudioTracks.ElementAt(0).Codec, true);
                Assert.AreEqual(2, assetMetadataArray[i].AudioTracks.ElementAt(0).Channels);
                Assert.AreEqual(48000, assetMetadataArray[i].AudioTracks.ElementAt(0).SamplingRate);
                Assert.AreEqual(128, assetMetadataArray[i].AudioTracks.ElementAt(0).Bitrate);
                ////Assert.AreEqual(0, assetMetadataArray[i].AudioTracks.ElementAt(0).BitsPerSample);
                Assert.AreEqual("und", assetMetadataArray[i].AudioTracks.ElementAt(0).Language, true);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowGenerateAssetFilesFromStorageIfAssetIsNull()
        {
            IAsset nullAsset = null;

            try
            {
                nullAsset.GenerateFromStorage();
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw;
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv")]
        public void ShouldGenerateAssetFilesFromBlobStorage()
        {
            var fileName = @"Media\smallwmv1.wmv";

            // Create empty asset.
            this.asset = this.context.Assets.Create(Path.GetFileNameWithoutExtension(fileName), AssetCreationOptions.None);

            // Create a SAS locator for the empty asset with write access.
            var sasLocator = this.context.Locators.Create(LocatorType.Sas, this.asset, AccessPermissions.Write, TimeSpan.FromDays(1));

            // Get a refence to the asset container in Blob storage.
            var locatorUri = new Uri(sasLocator.Path, UriKind.Absolute);
            var assetContainer = new CloudBlobContainer(locatorUri);

            // Upload a blob directly to the asset container.
            var blob = assetContainer.GetBlockBlobReference(fileName);
            blob.UploadFromStreamAsync(File.OpenRead(fileName)).Wait();

            // Refresh the asset reference.
            this.asset = this.context.Assets.Where(a => a.Id == this.asset.Id).First();

            Assert.AreEqual(0, this.asset.AssetFiles.Count());

            // Create the AssetFiles from Blob storage.
            this.asset.GenerateFromStorage();

            // Refresh the asset reference.
            this.asset = this.context.Assets.Where(a => a.Id == this.asset.Id).First();

            Assert.AreEqual(1, this.asset.AssetFiles.Count());
            Assert.AreEqual("smallwmv1.wmv", this.asset.AssetFiles.ToArray()[0].Name);
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowDownloadAssetFilesToFolderIfAssetIsNull()
        {
            IAsset nullAsset = null;

            string downloadFolderPath = CreateEmptyDirectory();

            try
            {
                Task downloadTask = nullAsset.DownloadToFolderAsync(downloadFolderPath, CancellationToken.None);

                downloadTask.Wait();
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw;
            }
            finally
            {
                DeleteDirectoryIfExists(downloadFolderPath);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowDownloadAssetFilesToFolderIfFolderPathDoesNotExist()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);

            string downloadFolderPath = CreateEmptyDirectory();

            try
            {
                this.asset.DownloadToFolder(downloadFolderPath);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
                throw;
            }
            finally
            {
                DeleteDirectoryIfExists(downloadFolderPath);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldDownloadAssetFilesToFolder()
        {
            var originalFolderPath = "Media";
            this.asset = this.context.Assets.CreateFromFolder(originalFolderPath, AssetCreationOptions.None);
            var assetId = this.asset.Id;

            string downloadFolderPath = CreateEmptyDirectory();

            try
            {
                this.asset.DownloadToFolder(downloadFolderPath);

                Assert.AreEqual(3, Directory.GetFiles(downloadFolderPath).Length);

                AssertDownloadedFile(originalFolderPath, downloadFolderPath, @"Media\smallwmv1.wmv");
                AssertDownloadedFile(originalFolderPath, downloadFolderPath, @"Media\smallwmv2.wmv");
                AssertDownloadedFile(originalFolderPath, downloadFolderPath, @"Media\dummy.ism");

                this.context = TestHelper.CreateContext();
                Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
            }
            finally
            {
                DeleteDirectoryIfExists(downloadFolderPath);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldDownloadAssetFilesToFolderWithDownloadProgressChangedCallback()
        {
            var originalFolderPath = "Media";
            this.asset = this.context.Assets.CreateFromFolder(originalFolderPath, AssetCreationOptions.None);
            var assetId = this.asset.Id;

            string downloadFolderPath = CreateEmptyDirectory();

            try
            {
                var downloadResults = new ConcurrentDictionary<string, DownloadProgressChangedEventArgs>();
                Action<IAssetFile, DownloadProgressChangedEventArgs> downloadProgressChangedCallback =
                    (af, e) =>
                    {
                        IAssetFile assetFile = af;
                        DownloadProgressChangedEventArgs eventArgs = e;

                        Assert.IsNotNull(assetFile);
                        Assert.IsNotNull(eventArgs);

                        downloadResults.AddOrUpdate(assetFile.Name, eventArgs, (k, e2) => eventArgs);
                    };

                this.asset.DownloadToFolder(downloadFolderPath, downloadProgressChangedCallback);

                Assert.AreEqual(3, downloadResults.Count);
                Assert.AreEqual(3, Directory.GetFiles(downloadFolderPath).Length);

                AssertDownloadedFile(originalFolderPath, downloadFolderPath, @"Media\smallwmv1.wmv", downloadResults[@"Media\smallwmv1.wmv"]);
                AssertDownloadedFile(originalFolderPath, downloadFolderPath, @"Media\smallwmv2.wmv", downloadResults[@"Media\smallwmv2.wmv"]);
                AssertDownloadedFile(originalFolderPath, downloadFolderPath, @"Media\dummy.ism", downloadResults[@"Media\dummy.ism"]);

                this.context = TestHelper.CreateContext();
                Assert.AreEqual(0, this.context.Locators.Where(l => l.AssetId == assetId).Count());
            }
            finally
            {
                DeleteDirectoryIfExists(downloadFolderPath);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldGetManifestAssetFile()
        {
            var folderName = "Media";
            this.asset = this.context.Assets.CreateFromFolder(folderName, AssetCreationOptions.None);

            var manifestAssetFile = this.asset.GetManifestAssetFile();

            Assert.IsNotNull(manifestAssetFile);
            Assert.AreEqual("dummy.ism", manifestAssetFile.Name);
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv")]
        public void ShouldGetManifestAssetFileReturnNullIfThereIsNoManifestFile()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\smallwmv1.wmv", AssetCreationOptions.None);

            var manifestAssetFile = this.asset.GetManifestAssetFile();

            Assert.IsNull(manifestAssetFile);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowGetManifestAssetFileIfAssetIsNull()
        {
            IAsset nullAsset = null;

            nullAsset.GetManifestAssetFile();
        }

        [TestMethod]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldGetSmoothStreamingUri()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

            var locator = this.context.Locators.Create(LocatorType.OnDemandOrigin, this.asset, AccessPermissions.Read, TimeSpan.FromDays(1));

            var smoothStreamingUrl = this.asset.GetSmoothStreamingUri();

            Assert.IsNotNull(smoothStreamingUrl);
            Assert.IsTrue(
                smoothStreamingUrl
                    .AbsoluteUri
                    .EndsWith(locator.ContentAccessComponent + "/dummy.ism/manifest", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldGetHlsUri()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

            var locator = this.context.Locators.Create(LocatorType.OnDemandOrigin, this.asset, AccessPermissions.Read, TimeSpan.FromDays(1));

            var hlsUri = this.asset.GetHlsUri();

            Assert.IsNotNull(hlsUri);
            Assert.IsTrue(
                hlsUri
                    .AbsoluteUri
                    .EndsWith(locator.ContentAccessComponent + "/dummy.ism/manifest(format=m3u8-aapl)", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldGetHlsv3Uri()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

            var locator = this.context.Locators.Create(LocatorType.OnDemandOrigin, this.asset, AccessPermissions.Read, TimeSpan.FromDays(1));

            var hlsv3Uri = this.asset.GetHlsv3Uri();

            Assert.IsNotNull(hlsv3Uri);
            Assert.IsTrue(
                hlsv3Uri
                    .AbsoluteUri
                    .EndsWith(locator.ContentAccessComponent + "/dummy.ism/manifest(format=m3u8-aapl-v3)", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [DeploymentItem(@"Media\dummy.ism")]
        public void ShouldGetMpegDashUri()
        {
            this.asset = this.context.Assets.CreateFromFile(@"Media\dummy.ism", AssetCreationOptions.None);

            var locator = this.context.Locators.Create(LocatorType.OnDemandOrigin, this.asset, AccessPermissions.Read, TimeSpan.FromDays(1));

            var mpegDashUri = this.asset.GetMpegDashUri();

            Assert.IsNotNull(mpegDashUri);
            Assert.IsTrue(
                mpegDashUri
                    .AbsoluteUri
                    .EndsWith(locator.ContentAccessComponent + "/dummy.ism/manifest(format=mpd-time-csf)", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowGetStreamingUriIfAssetIsNull()
        {
            IAsset nullAsset = null;

            nullAsset.GetSmoothStreamingUri();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ShouldThrowGetMediaContextIfAssetIsNull()
        {
            IAsset nullAsset = null;

            nullAsset.GetMediaContext();
        }

        [TestMethod]
        public void ShouldGetMediaContext()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);

            var mediaContext = this.asset.GetMediaContext();

            Assert.IsNotNull(mediaContext);
            Assert.AreSame(this.context, mediaContext);
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowCopyIfAssetIsNull()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);
            IAsset nullAsset = null;
            var storageCredentials = TestHelper.CreateStorageCredentials();

            try
            {
                nullAsset.Copy(this.asset, storageCredentials);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowCopyIfDestinationAssetIsNull()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);
            IAsset nullAsset = null;
            var storageCredentials = TestHelper.CreateStorageCredentials();

            try
            {
                this.asset.Copy(nullAsset, storageCredentials);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowCopyIfStorageCredentialsIsNull()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);
            this.outputAsset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            StorageCredentials nullStorageCredentials = null;

            try
            {
                this.asset.Copy(this.outputAsset, nullStorageCredentials);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentNullException));
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowCopyIfStorageCredentialsIsAnonymous()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);
            this.outputAsset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            StorageCredentials storageCredentials = new StorageCredentials();

            try
            {
                this.asset.Copy(this.outputAsset, storageCredentials);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowCopyIfStorageCredentialsIsSAS()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);
            this.outputAsset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            StorageCredentials storageCredentials = new StorageCredentials("?se=2015-05-22T19%3A46%3A16Z&sr=c&si=efa38601-1f8e-4e3a-9a85-2485e2a4f374&sv=2012-02-12&sig=CwUScO98yTHKNRdzwJNRIB7BhRHc9fg4ng1Bb0KE0vo%3D");

            try
            {
                this.asset.Copy(this.outputAsset, storageCredentials);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void ShouldThrowCopyIfStorageCredentialsDoesNotBelongToDestinationAssetStorageAccount()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);
            this.outputAsset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            StorageCredentials storageCredentials = new StorageCredentials("anotherstorageaccountname", "2d3mDy7UXbW47f4qZAXu+jNd/ZEQifENiz74G/kcfr4rKUIIWcOUtqwvE4Bb7bporT1L4Q7V/lZXdiHDZyVnIw==");

            try
            {
                this.asset.Copy(this.outputAsset, storageCredentials);
            }
            catch (AggregateException exception)
            {
                Assert.IsInstanceOfType(exception.InnerException, typeof(ArgumentException));
                throw;
            }
        }

        [TestMethod]
        public void ShouldCopyIfAssetIsEmpty()
        {
            this.asset = this.context.Assets.Create("empty", AssetCreationOptions.None);
            this.outputAsset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            var storageCredentials = TestHelper.CreateStorageCredentials();

            this.asset.Copy(this.outputAsset, storageCredentials);

            Assert.AreEqual(0, this.asset.AssetFiles.Count());
            Assert.AreEqual(0, this.outputAsset.AssetFiles.Count());

            Assert.AreEqual(0, this.asset.Locators.Count());
            Assert.AreEqual(0, this.outputAsset.Locators.Count());
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldCopyAsset()
        {
            var folderName = "Media";
            this.asset = this.context.Assets.CreateFromFolder(folderName, AssetCreationOptions.None);
            this.outputAsset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            var storageCredentials = TestHelper.CreateStorageCredentials();

            // Workaround for SAS locator creation delay
            Thread.Sleep(20000);

            this.asset.Copy(this.outputAsset, storageCredentials);

            var sourceAssetFiles = this.asset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            var destinationAssetFiles = this.outputAsset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            Assert.AreEqual(sourceAssetFiles.Length, destinationAssetFiles.Length);

            for (int i = 0; i < sourceAssetFiles.Length; i++)
            {
                var sourceAssetFile = sourceAssetFiles[i];
                var destinationAssetFile = destinationAssetFiles[i];

                Assert.AreEqual(destinationAssetFile.Name, destinationAssetFile.Name);
                Assert.AreEqual(destinationAssetFile.ContentFileSize, destinationAssetFile.ContentFileSize);
                Assert.AreEqual(destinationAssetFile.MimeType, destinationAssetFile.MimeType);
                Assert.AreEqual(destinationAssetFile.IsPrimary, destinationAssetFile.IsPrimary);
            }

            Assert.AreEqual(0, this.asset.Locators.Count());
            Assert.AreEqual(0, this.outputAsset.Locators.Count());
        }

        [TestMethod]
        [DeploymentItem(@"Media\smallwmv1.wmv", "Media")]
        [DeploymentItem(@"Media\smallwmv2.wmv", "Media")]
        [DeploymentItem(@"Media\dummy.ism", "Media")]
        public void ShouldCopyAssetTwice()
        {
            var folderName = "Media";
            this.asset = this.context.Assets.CreateFromFolder(folderName, AssetCreationOptions.None);
            this.outputAsset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            var storageCredentials = TestHelper.CreateStorageCredentials();

            // Workaround for SAS locator creation delay
            Thread.Sleep(20000);

            // Copy to an empty asset
            this.asset.Copy(this.outputAsset, storageCredentials);

            var sourceAssetFiles = this.asset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            var destinationAssetFiles = this.outputAsset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            Assert.AreEqual(sourceAssetFiles.Length, destinationAssetFiles.Length);

            for (int i = 0; i < sourceAssetFiles.Length; i++)
            {
                var sourceAssetFile = sourceAssetFiles[i];
                var destinationAssetFile = destinationAssetFiles[i];

                Assert.AreEqual(destinationAssetFile.Name, destinationAssetFile.Name);
                Assert.AreEqual(destinationAssetFile.ContentFileSize, destinationAssetFile.ContentFileSize);
                Assert.AreEqual(destinationAssetFile.MimeType, destinationAssetFile.MimeType);
                Assert.AreEqual(destinationAssetFile.IsPrimary, destinationAssetFile.IsPrimary);
            }

            Assert.AreEqual(0, this.asset.Locators.Count());
            Assert.AreEqual(0, this.outputAsset.Locators.Count());

            // Workaround for SAS locator creation delay
            Thread.Sleep(25000);

            // Copy to an asset with existing content
            this.asset.Copy(this.outputAsset, storageCredentials);

            sourceAssetFiles = this.asset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            destinationAssetFiles = this.outputAsset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            Assert.AreEqual(sourceAssetFiles.Length, destinationAssetFiles.Length);

            for (int i = 0; i < sourceAssetFiles.Length; i++)
            {
                var sourceAssetFile = sourceAssetFiles[i];
                var destinationAssetFile = destinationAssetFiles[i];

                Assert.AreEqual(destinationAssetFile.Name, destinationAssetFile.Name);
                Assert.AreEqual(destinationAssetFile.ContentFileSize, destinationAssetFile.ContentFileSize);
                Assert.AreEqual(destinationAssetFile.MimeType, destinationAssetFile.MimeType);
                Assert.AreEqual(destinationAssetFile.IsPrimary, destinationAssetFile.IsPrimary);
            }

            Assert.AreEqual(0, this.asset.Locators.Count());
            Assert.AreEqual(0, this.outputAsset.Locators.Count());
        }

        [TestMethod]
        public void ShouldCopyFragBlobAsset()
        {
            var fragBlobAsset = this.context.Assets.Where(a => a.Id == TestHelper.FragBlobAssetId).First();
            this.asset = this.context.Assets.Create("copy", AssetCreationOptions.None);
            var storageCredentials = TestHelper.CreateStorageCredentials();

            fragBlobAsset.Copy(this.asset, storageCredentials);

            var sourceAssetFiles = fragBlobAsset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            var destinationAssetFiles = this.asset.AssetFiles.ToArray().OrderBy(af => af.Name).ToArray();
            Assert.AreEqual(sourceAssetFiles.Length, destinationAssetFiles.Length);

            for (int i = 0; i < sourceAssetFiles.Length; i++)
            {
                var sourceAssetFile = sourceAssetFiles[i];
                var destinationAssetFile = destinationAssetFiles[i];

                Assert.AreEqual(destinationAssetFile.Name, destinationAssetFile.Name);
                Assert.AreEqual(destinationAssetFile.ContentFileSize, destinationAssetFile.ContentFileSize);
                Assert.AreEqual(destinationAssetFile.MimeType, destinationAssetFile.MimeType);
                Assert.AreEqual(destinationAssetFile.IsPrimary, destinationAssetFile.IsPrimary);
            }

            Assert.AreEqual(0, this.asset.Locators.Count());
        }

        [TestInitialize]
        public void Initialize()
        {
            this.context = TestHelper.CreateContext();
            this.asset = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (this.asset != null)
            {
                this.asset.Delete();
            }

            if (this.outputAsset != null)
            {
                this.outputAsset.Delete();
            }
        }

        private static void AssertDownloadedFile(string originalFolderPath, string downloadFolderPath, string fileName, DownloadProgressChangedEventArgs downloadProgressChangedEventArgs = null)
        {
            var expected = new FileInfo(Path.Combine(originalFolderPath, fileName));
            var result = new FileInfo(Path.Combine(downloadFolderPath, fileName));

            Assert.AreEqual(expected.Length, result.Length);

            if (downloadProgressChangedEventArgs != null)
            {
                Assert.AreEqual(expected.Length, downloadProgressChangedEventArgs.BytesDownloaded);
                Assert.AreEqual(expected.Length, downloadProgressChangedEventArgs.TotalBytes);
                Assert.AreEqual(100, downloadProgressChangedEventArgs.Progress);
            }
        }

        private static string CreateEmptyDirectory()
        {
            var downloadFolderPath = Path.GetRandomFileName();

            DeleteDirectoryIfExists(downloadFolderPath);

            Directory.CreateDirectory(downloadFolderPath);

            return downloadFolderPath;
        }

        private static void DeleteDirectoryIfExists(string downloadFolderPath)
        {
            if (downloadFolderPath != null)
            {
                if (Directory.Exists(downloadFolderPath))
                {
                    Directory.Delete(downloadFolderPath, true);
                }
            }
        }
    }
}
