using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Verse;

namespace CAP_ChatInteractive
{
    public class YouTubeOAuthService
    {
        private readonly StreamServiceSettings _settings;
        private UserCredential _credential;

        // OAuth scopes needed for YouTube Live Chat
        private static readonly string[] Scopes = {
            YouTubeService.Scope.Youtube,
            YouTubeService.Scope.YoutubeForceSsl,
            YouTubeService.Scope.Youtubepartner
        };

        public bool IsAuthenticated => _credential != null && !_credential.Token.IsStale;

        public YouTubeOAuthService(StreamServiceSettings settings)
        {
            _settings = settings;
        }

        public bool Authenticate()
        {
            try
            {
                Logger.YouTube("Starting YouTube OAuth authentication...");

                string clientSecretsPath = JsonFileManager.GetFilePath("client_secrets.json");

                // Check if file exists
                if (!JsonFileManager.FileExists("client_secrets.json"))
                {
                    Logger.Warning("client_secrets.json not found. OAuth authentication will fail.");
                    Logger.Message("Use the YouTube settings to create the client_secrets.json file");
                    return false;
                }

                string credPath = JsonFileManager.GetFilePath("youtube_credentials.json");

                using (var stream = new FileStream(clientSecretsPath, FileMode.Open, FileAccess.Read))
                {
                    _credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        Scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true)).Result;
                }

                Logger.YouTube("YouTube OAuth authentication successful!");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"YouTube OAuth authentication failed: {ex.Message}");
                return false;
            }
        }

        public YouTubeService CreateAuthenticatedService()
        {
            if (!IsAuthenticated && !Authenticate())
            {
                throw new InvalidOperationException("YouTube authentication required");
            }

            return new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "CAP Chat Interactive"
            });
        }

        public void RefreshTokenIfNeeded()
        {
            if (_credential?.Token?.IsStale == true)
            {
                Logger.YouTube("Refreshing YouTube OAuth token...");
                _credential.RefreshTokenAsync(CancellationToken.None).Wait();
            }
        }
    }
}