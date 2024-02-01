﻿using FluentFTP;
using System.Text.Json;

namespace JobsWorkerWebService.Server.Extensions
{
    public static class AsyncFtpClientExtensions
    {
        public static async Task<string?> DownloadAsString(
    this AsyncFtpClient asyncFtpClient,
    string remotePath, ILogger logger,
    CancellationToken cancellationToken = default)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                using var streamReader = new StreamReader(memoryStream);
                if (!await asyncFtpClient.DownloadStream(memoryStream, remotePath, 0, null, cancellationToken))
                {
                    return null;
                }
                memoryStream.Position = 0;
                return streamReader.ReadToEnd();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.ToString());
            }
            return null;
        }

        public static async Task<T?> DownloadAsJson<T>(
            this AsyncFtpClient asyncFtpClient,
            string remotePath, ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                if (!await asyncFtpClient.DownloadStream(memoryStream, remotePath, 0, null, cancellationToken))
                {
                    return default(T);
                }
                memoryStream.Position = 0;
                return JsonSerializer.Deserialize<T>(memoryStream);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.ToString());
            }
            return default(T?);
        }

    }
}
