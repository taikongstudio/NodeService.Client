﻿using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorUploadTool.Helper
{
    public static class CompressionHelper
    {
        public static async Task<MemoryStream> CompressFileAsync(string fileName)
        {
            using FileStream originalFileStream = File.Open(fileName, FileMode.Open);
            var memoryStream = new MemoryStream((int)originalFileStream.Length);
            using var compressor = new GZipStream(memoryStream, CompressionMode.Compress, true);
            await originalFileStream.CopyToAsync(compressor);
            await originalFileStream.FlushAsync();
            await compressor.FlushAsync();
            return memoryStream;
        }

        public static async Task DecompressFileAsync(string compressedFileName, string decompressedFileName)
        {
            using FileStream compressedFileStream = File.Open(compressedFileName, FileMode.Open);
            using FileStream outputFileStream = File.Create(decompressedFileName);
            using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
            await decompressor.CopyToAsync(outputFileStream);
        }

    }
}