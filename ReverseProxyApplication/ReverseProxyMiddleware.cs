using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace ReverseProxyApplication
{
    public class ReverseProxyMiddleware
    {
        public ReverseProxyMiddleware(RequestDelegate nextMiddleware) {}

        public async Task Invoke(HttpContext context) {
            if (context.Request.Path.ToString().EndsWith("/DWADragonsMain.xml")) {
                if (context.Request.Path.ToString().EndsWith("3.12.0/DWADragonsMain.xml"))
                    await SendMainXml(context, "3.12");
                else
                    await SendMainXml(context, "1.13");
                return;
            }

            Uri targetUri;
            PathString remainingPath;
            if (context.Request.Path.StartsWithSegments("/apiproxy", out remainingPath)) {
                targetUri = new Uri("https://api.sodoff.spirtix.com" + remainingPath);
            } else {
                if (!context.Request.Path.StartsWithSegments("/sproxy.com", out remainingPath))
                    remainingPath = context.Request.Path;
                targetUri = new Uri("https://media.sodoff.spirtix.com" + remainingPath);
            }

            HttpClient httpClient = new HttpClient();
            var targetRequestMessage = CreateTargetMessage(context, targetUri);
            using (var responseMessage = await httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
            {
                context.Response.StatusCode = (int)responseMessage.StatusCode;
                CopyFromTargetResponseHeaders(context, responseMessage);
                await WriteResponseContent(context, await responseMessage.Content.ReadAsByteArrayAsync());
            }
        }

        private async Task SendMainXml(HttpContext context, string version) {
            var assembly = Assembly.GetExecutingAssembly();
            Stream stream = assembly.GetManifestResourceStream(
                assembly.GetManifestResourceNames().Single(str => str.EndsWith("DWADragonsMain-" + version + ".xml"))
            );
            MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            
            context.Response.StatusCode = 200;
            await WriteResponseContent(context, ms.ToArray());
        }
        
        private async Task WriteResponseContent(HttpContext context, Byte[] data) {
            // NOTE: loop with 8192 buffer size write + flush operations (instead of simple `context.Response.Body.WriteAsync(data)`)
            //       to avoid `System.Net.Sockets.SocketException (10040): Unknown error (0x2738)` on some systems
            for (int i=0, len=8192; i<data.Length; i+=len) {
                if (i+len > data.Length)
                    len = data.Length - i;
                await context.Response.Body.WriteAsync(data, i, len);
                await context.Response.Body.FlushAsync();
            }
        }

        private HttpRequestMessage CreateTargetMessage(HttpContext context, Uri targetUri)
        {
            var requestMessage = new HttpRequestMessage();
            CopyFromOriginalRequestContentAndHeaders(context, requestMessage);

            requestMessage.RequestUri = targetUri;
            requestMessage.Headers.Host = targetUri.Host;
            requestMessage.Method = GetMethod(context.Request.Method);
           
            return requestMessage;
        }

        private void CopyFromOriginalRequestContentAndHeaders(HttpContext context, HttpRequestMessage requestMessage)
        {
            var requestMethod = context.Request.Method;

            if (!HttpMethods.IsGet(requestMethod) &&
                !HttpMethods.IsHead(requestMethod) &&
                !HttpMethods.IsDelete(requestMethod) &&
                !HttpMethods.IsTrace(requestMethod))
            {
                var streamContent = new StreamContent(context.Request.Body);
                requestMessage.Content = streamContent;
            }

            foreach (var header in context.Request.Headers)
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        private void CopyFromTargetResponseHeaders(HttpContext context, HttpResponseMessage responseMessage)
        {
            foreach (var header in responseMessage.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }
            context.Response.Headers.Remove("transfer-encoding");
        }
        
        private static HttpMethod GetMethod(string method)
        {
            if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
            if (HttpMethods.IsGet(method)) return HttpMethod.Get;
            if (HttpMethods.IsHead(method)) return HttpMethod.Head;
            if (HttpMethods.IsOptions(method)) return HttpMethod.Options;
            if (HttpMethods.IsPost(method)) return HttpMethod.Post;
            if (HttpMethods.IsPut(method)) return HttpMethod.Put;
            if (HttpMethods.IsTrace(method)) return HttpMethod.Trace;
            return new HttpMethod(method);
        }
    }
}
