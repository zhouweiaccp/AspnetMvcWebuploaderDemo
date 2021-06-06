using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace WebUploaderDemo.Controllers
{//https://stackoverflow.com/questions/49769853/dropzone-js-chunking
    public class ChunkedUploadController : ApiController
    {
        private class DzMeta
        {
            public int intChunkNumber = 0;
            public string dzChunkNumber { get; set; }
            public string dzChunkSize { get; set; }
            public string dzCurrentChunkSize { get; set; }
            public string dzTotalSize { get; set; }
            public string dzIdentifier { get; set; }
            public string dzFilename { get; set; }
            public string dzTotalChunks { get; set; }
            public string dzCurrentChunkByteOffset { get; set; }
            public string userID { get; set; }

            public DzMeta(Dictionary<string, string> values)
            {
                dzChunkNumber = values["dzChunkIndex"];
                dzChunkSize = values["dzChunkSize"];
                dzCurrentChunkSize = values["dzCurrentChunkSize"];
                dzTotalSize = values["dzTotalFileSize"];
                dzIdentifier = values["dzUuid"];
                dzFilename = values["dzFileName"];
                dzTotalChunks = values["dzTotalChunkCount"];
                dzCurrentChunkByteOffset = values["dzChunkByteOffset"];
                userID = values["userID"];
                int.TryParse(dzChunkNumber, out intChunkNumber);
            }

            public DzMeta(NameValueCollection values)
            {
                dzChunkNumber = values["dzChunkIndex"];
                dzChunkSize = values["dzChunkSize"];
                dzCurrentChunkSize = values["dzCurrentChunkSize"];
                dzTotalSize = values["dzTotalFileSize"];
                dzIdentifier = values["dzUuid"];
                dzFilename = values["dzFileName"];
                dzTotalChunks = values["dzTotalChunkCount"];
                dzCurrentChunkByteOffset = values["dzChunkByteOffset"];
                userID = values["userID"];
                int.TryParse(dzChunkNumber, out intChunkNumber);
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> UploadChunk()
        {
            HttpResponseMessage response = new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.Created };

            try
            {
                if (!Request.Content.IsMimeMultipartContent("form-data"))
                {
                    //No Files uploaded
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    response.Content = new StringContent("No file uploaded or MIME multipart content not as expected!");
                    throw new HttpResponseException(response);
                }

                var meta = new DzMeta(HttpContext.Current.Request.Form);
                var chunkDirBasePath = tSysParm.GetParameter("CHUNKUPDIR");
                var path = string.Format(@"{0}\{1}", chunkDirBasePath, meta.dzIdentifier);
                var filename = string.Format(@"{0}.{1}.{2}.tmp", meta.dzFilename, (meta.intChunkNumber + 1).ToString().PadLeft(4, '0'), meta.dzTotalChunks.PadLeft(4, '0'));
                Directory.CreateDirectory(path);

                Request.Content.LoadIntoBufferAsync().Wait();

                await Request.Content.ReadAsMultipartAsync(new CustomMultipartFormDataStreamProvider(path, filename)).ContinueWith((task) =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        response.StatusCode = HttpStatusCode.InternalServerError;
                        response.Content = new StringContent("Chunk upload task is faulted or canceled!");
                        throw new HttpResponseException(response);
                    }
                });
            }
            catch (HttpResponseException ex)
            {
                LogProxy.WriteError(ex.Response.Content.ToString(), ex);
            }
            catch (Exception ex)
            {
                LogProxy.WriteError("Error uploading/saving chunk to filesystem", ex);
                response.StatusCode = HttpStatusCode.InternalServerError;
                response.Content = new StringContent(string.Format("Error uploading/saving chunk to filesystem: {0}", ex.Message));
            }

            return response;
        }

        [HttpPut]
        public HttpResponseMessage CommitChunks([FromUri] string dzIdentifier, [FromUri] string fileName, [FromUri] int expectedBytes, [FromUri] int totalChunks, [FromUri] int userID)
        {
            HttpResponseMessage response = new HttpResponseMessage { StatusCode = HttpStatusCode.OK };
            string path = "";

            try
            {
                var chunkDirBasePath = tSysParm.GetParameter("CHUNKUPDIR");
                path = string.Format(@"{0}\{1}", chunkDirBasePath, dzIdentifier);
                var dest = Path.Combine(path, HttpUtility.UrlDecode(fileName));
                FileInfo info = null;

                // Get all files in directory and combine in filestream
                var files = Directory.EnumerateFiles(path).Where(s => !s.Equals(dest)).OrderBy(s => s);
                // Check that the number of chunks is as expected
                if (files.Count() != totalChunks)
                {
                    response.Content = new StringContent(string.Format("Total number of chunks: {0}. Expected: {1}!", files.Count(), totalChunks));
                    throw new HttpResponseException(response);
                }

                // Merge chunks into one file
                using (var fStream = new FileStream(dest, FileMode.Create))
                {
                    foreach (var file in files)
                    {
                        using (var sourceStream = System.IO.File.OpenRead(file))
                        {
                            sourceStream.CopyTo(fStream);
                        }
                    }
                    fStream.Flush();
                }

                // Check that merged file length is as expected.
                info = new FileInfo(dest);
                if (info != null)
                {
                    if (info.Length == expectedBytes)
                    {
                        // Save the file in the database
                        tTempAtt file = tTempAtt.NewInstance();
                        file.ContentType = MimeMapping.GetMimeMapping(info.Name);
                        file.File = System.IO.File.ReadAllBytes(info.FullName);
                        file.FileName = info.Name;
                        file.Title = info.Name;
                        file.TemporaryID = userID;
                        file.Description = info.Name;
                        file.User = userID;
                        file.Date = SafeDateTime.Now;
                        file.Insert();
                    }
                    else
                    {
                        response.Content = new StringContent(string.Format("Total file size: {0}. Expected: {1}!", info.Length, expectedBytes));
                        throw new HttpResponseException(response);
                    }
                }
                else
                {
                    response.Content = new StringContent("Chunks failed to merge and file not saved!");
                    throw new HttpResponseException(response);
                }
            }
            catch (HttpResponseException ex)
            {
               // LogProxy.WriteError(ex.Response.Content.ToString(), ex);
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
            }
            catch (Exception ex)
            {
               // LogProxy.WriteError("Error merging chunked upload!", ex);
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                response.Content = new StringContent(string.Format("Error merging chunked upload: {0}", ex.Message));
            }
            finally
            {
                // No matter what happens, we need to delete the temporary files if they exist
                if ( Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }

            return response;
        }

        [HttpDelete]
        public HttpResponseMessage DeleteCanceledChunks([FromUri] string dzIdentifier, [FromUri] string fileName, [FromUri] int expectedBytes, [FromUri] int totalChunks, [FromUri] int userID)
        {
            HttpResponseMessage response = new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK };

            try
            {
                var chunkDirBasePath = "";// tSysParm.GetParameter("CHUNKUPDIR");
                var path = string.Format(@"{0}\{1}", chunkDirBasePath, dzIdentifier);

                // Delete abandoned chunks if they exist
                if ( Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                //LogProxy.WriteError("Error deleting canceled chunks", ex);
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                response.Content = new StringContent(string.Format("Error deleting canceled chunks: {0}", ex.Message));
            }

            return response;
        }
    }
    public class CustomMultipartFormDataStreamProvider : MultipartFormDataStreamProvider
    {
        public readonly string _filename;
        public CustomMultipartFormDataStreamProvider(string path, string filename) : base(path)
        {
            _filename = filename;
        }

        public override string GetLocalFileName(System.Net.Http.Headers.HttpContentHeaders headers)
        {
            return _filename;
        }
    }
}
