﻿//using ComponentAce.Compression.Libs.zlib;
using ComponentAce.Compression.Libs.zlib;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Paulov.Launcher
{
    public class TarkovRequesting
    {
        public string Session;
        public string RemoteEndPoint;
        public bool isUnity;

        private static HttpClient httpClient;

        public TarkovRequesting(string session, string remoteEndPoint, bool isUnity = true)
        {
            Session = session;
            RemoteEndPoint = remoteEndPoint;
            httpClient = new()
            {
                BaseAddress = new Uri(RemoteEndPoint),
            };

            httpClient.DefaultRequestHeaders.Add("Cookie", $"PHPSESSID={Session}");
            httpClient.DefaultRequestHeaders.Add("SessionId", Session);
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "deflate");
            httpClient.Timeout = new TimeSpan(0, 0, 1);
        }

        //private static byte[] CompressFile(Stream stream)
        //{
        //    MemoryStream ms = new MemoryStream();
        //    GZipStream deflateStream;
        //    using (deflateStream = new GZipStream(ms, CompressionMode.Compress))
        //    {
        //        stream.CopyTo(deflateStream);
        //    }
        //    return ms.ToArray();
        //}

        //private static byte[] DecompressFile(byte[] bytes)
        //{
        //    var str = UTF8Encoding.UTF8.GetString(bytes);

        //    var destination = new MemoryStream();
        //    var instream = new MemoryStream(bytes);
        //    using (var decompressor = (Stream)new DeflateStream(instream, CompressionMode.Decompress, true))
        //    {
        //        decompressor.CopyTo(destination);
        //    }

        //    destination.Seek(0, SeekOrigin.Begin);

        //    return destination.ToArray();
        //}

        //private static void DecompressFile()
        //{
        //    using FileStream compressedFileStream = File.Open(CompressedFileName, FileMode.Open);
        //    using FileStream outputFileStream = File.Create(DecompressedFileName);
        //    using var decompressor = new DeflateStream(compressedFileStream, CompressionMode.Decompress);
        //    decompressor.CopyTo(outputFileStream);
        //}

        /// <summary>
        /// Send request to the server and get Stream of data back
        /// </summary>
        /// <param name="url">String url endpoint example: /start</param>
        /// <param name="method">POST or GET</param>
        /// <param name="data">string json data</param>
        /// <param name="compress">Should use compression gzip?</param>
        /// <returns>Stream or null</returns>
        private (Stream, WebResponse) Send(string url, string method = "GET", string data = null, bool compress = true)
        {
            // disable SSL encryption
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            var fullUri = url;
            if (!Uri.IsWellFormedUriString(fullUri, UriKind.Absolute))
                fullUri = RemoteEndPoint + fullUri;

            if (!fullUri.StartsWith("https://") && !fullUri.StartsWith("http://"))
                fullUri = fullUri.Insert(0, "https://");

            WebRequest request = WebRequest.Create(new Uri(fullUri));

            if (!string.IsNullOrEmpty(Session))
            {
                request.Headers.Add("Cookie", $"PHPSESSID={Session}");
                request.Headers.Add("SessionId", Session);
            }

            //request.Headers.Add("Accept-Encoding", "deflate");
            request.Headers.Add("Accept-Encoding", "deflate, gzip");

            request.Method = method;
            request.Timeout = (int)Math.Round(new TimeSpan(0, 1, 0).TotalMilliseconds);

            if (method != "GET" && !string.IsNullOrEmpty(data))
            {
                byte[] bytes = (compress) ? SimpleZlib.CompressToBytes(data, zlibConst.Z_BEST_SPEED) : Encoding.UTF8.GetBytes(data);

                request.ContentType = "application/json";
                request.ContentLength = bytes.Length;

                if (compress)
                {
                    request.Headers.Add("content-encoding", "deflate");
                }

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            // get response stream
            try
            {
                var response = request.GetResponse();
                return (response.GetResponseStream(), response);
            }
            catch (Exception ex)
            {
                MessageBox.Show(url + " " + ex.Message);
                return SendHttp(url, method, data, compress);
            }
        }


        private (Stream, WebResponse) SendHttp(string url, string method = "GET", string data = null, bool compress = true)
        {
            // disable SSL encryption
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            var fullUri = url;
            if (!Uri.IsWellFormedUriString(fullUri, UriKind.Absolute))
                fullUri = RemoteEndPoint + fullUri;

            if (!fullUri.StartsWith("http://"))
                fullUri = fullUri.Insert(0, "http://");

            WebRequest request = WebRequest.Create(new Uri(fullUri));

            if (!string.IsNullOrEmpty(Session))
            {
                request.Headers.Add("Cookie", $"PHPSESSID={Session}");
                request.Headers.Add("SessionId", Session);
            }

            request.Headers.Add("Accept-Encoding", "deflate");

            request.Method = method;
            request.Timeout = 5000;

            if (method != "GET" && !string.IsNullOrEmpty(data))
            {
                byte[] bytes = (compress) ? SimpleZlib.CompressToBytes(data, zlibConst.Z_BEST_COMPRESSION) : Encoding.UTF8.GetBytes(data);

                request.ContentType = "application/json";
                request.ContentLength = bytes.Length;

                if (compress)
                {
                    request.Headers.Add("content-encoding", "deflate");
                }

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }

            // get response stream
            var response = request.GetResponse();
            return (response.GetResponseStream(), response);

        }

        public void PutJson(string url, string data, bool compress = true)
        {
            using (Stream stream = Send(url, "PUT", data, compress).Item1) { }
        }

        public string GetJson(string url, bool compress = true)
        {
            using (Stream stream = Send(url, "GET", null, compress).Item1)
            {
                using (MemoryStream ms = new())
                {
                    if (stream == null)
                        return "";
                    stream.CopyTo(ms);
                    //return Encoding.UTF8.GetString(DecompressFile(ms.ToArray()));
                    return SimpleZlib.Decompress(ms.ToArray(), null);
                }
            }
        }

        public string PostJson(string url, string data, bool compress = true)
        {
            var postItems = Send(url, "POST", data, compress);
            var stream = postItems.Item1;
            var response = postItems.Item2;
            using (stream)
            {
                using (MemoryStream ms = new())
                {
                    if (stream == null)
                        return "";
                    stream.CopyTo(ms);
                    //return Encoding.UTF8.GetString(DecompressFile(ms.ToArray()));

                    //if (
                    //    (response.Headers[HttpRequestHeader.ContentEncoding] != null && response.Headers[HttpRequestHeader.ContentEncoding] == "deflate")
                    //    || (response.Headers[HttpRequestHeader.TransferEncoding] != null && response.Headers[HttpRequestHeader.TransferEncoding] == "chunked")
                    //    )
                    if (compress)
                        return SimpleZlib.Decompress(ms.ToArray(), null);
                    else
                        return Encoding.UTF8.GetString(ms.ToArray());


                }
            }
        }


    }
}
