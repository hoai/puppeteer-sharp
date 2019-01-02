﻿using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PuppeteerSharp.Helpers.Linux;
using Xunit;
using Xunit.Abstractions;
using PuppeteerSharp.Helpers;

namespace PuppeteerSharp.Tests.PuppeteerTests
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class BrowserFetcherTests : PuppeteerBaseTest
    {
        private readonly string _downloadsFolder;

        public BrowserFetcherTests(ITestOutputHelper output) : base(output)
        {
            _downloadsFolder = Path.Combine(Directory.GetCurrentDirectory(), ".test-chromium");
            EnsureDownloadsFolderIsDeleted();
        }

        [Fact]
        public async Task ShouldDownloadAndExtractLinuxBinary()
        {
            var browserFetcher = Puppeteer.CreateBrowserFetcher(new BrowserFetcherOptions
            {
                Platform = Platform.Linux,
                Path = _downloadsFolder,
                Host = TestConstants.ServerUrl
            });
            var revisionInfo = browserFetcher.RevisionInfo(123456);

            Server.SetRedirect(revisionInfo.Url.Substring(TestConstants.ServerUrl.Length), "/chromium-linux.zip");
            Assert.False(revisionInfo.Local);
            Assert.Equal(Platform.Linux, revisionInfo.Platform);
            Assert.False(await browserFetcher.CanDownloadAsync(100000));
            Assert.True(await browserFetcher.CanDownloadAsync(123456));

            try
            {
                revisionInfo = await browserFetcher.DownloadAsync(123456);
                Assert.True(revisionInfo.Local);
                Assert.Equal("LINUX BINARY\n", File.ReadAllText(revisionInfo.ExecutablePath));

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Assert.Equal(
                        BrowserFetcher.BrowserPermissionsInLinux,
                        LinuxSysCall.GetFileMode(revisionInfo.ExecutablePath) & BrowserFetcher.BrowserPermissionsInLinux);
                }
                Assert.Equal(new[] { 123456 }, browserFetcher.LocalRevisions());
                browserFetcher.Remove(123456);
                Assert.Empty(browserFetcher.LocalRevisions());

                //Download should return data from a downloaded version
                //This section is not in the Puppeteer test.
                await browserFetcher.DownloadAsync(123456);
                Server.Reset();
                revisionInfo = await browserFetcher.DownloadAsync(123456);
                Assert.True(revisionInfo.Local);
                Assert.Equal("LINUX BINARY\n", File.ReadAllText(revisionInfo.ExecutablePath));
            }
            finally
            {
                EnsureDownloadsFolderIsDeleted();
            }
        }
        private void EnsureDownloadsFolderIsDeleted()
        {
            if (Directory.Exists(_downloadsFolder))
            {
                Directory.Delete(_downloadsFolder, true);
            }
        }

        [Fact]
        public async Task ShouldUseWebProxy()
        {
            var tcs = new TaskCompletionSource<bool>();

            var browserFetcher = new BrowserFetcher()
            {
                Proxy = new WebProxy(TestConstants.HttpsPrefix)
            };

            HttpsServer.SetRoute(string.Empty, context =>
            {
                if (context.Request.Host.ToString().Contains(new Uri(browserFetcher.DownloadHost).DnsSafeHost))
                {
                    tcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            });
            var _ = browserFetcher.DownloadAsync(-1);
            await tcs.Task.WithTimeout();
        }
    }
}