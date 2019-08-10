using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace WebUploaderDemo.App_Start
{
    //没有完成功能
    //     1.数据管道解析
    //     2.进度条功能
    //     3.nuget 上传，自动打包
    //     3.mvc各版本适配  mvc4,mvc5 
    //     4.模块分离，1缓存，2存储
    //     5.
    //待研究
    //        1.大文件上传【4G,5G】，切几个段，在分片，[html5 FormData]  https://blog.csdn.net/x746655242/article/details/52964623
    //待开发功能
    //     1.存储方式切换，如分块，对接其他存储s3  flastfs   tfs(淘宝)
    /********
     * 
     * 
     * 
     * 
     * 
     * 
     * 
     * 
     * 
     * http://cutesoft.net/forums/48/ShowForum.aspx
     * https://www.cnblogs.com/Byrd/archive/2011/05/09/2040959.html
     * *********/

    /// <summary>
    /// 
    /// </summary>
    public class MulitUploadFilModule : IHttpModule
    {
        public void Dispose()
        {
            //throw new NotImplementedException();
        }



        #region MyRegion
        private byte[] ExtractBoundary(string contentType, Encoding encoding)
        {
            int num1 = contentType.IndexOf("boundary=");
            if (num1 > 0)
            {
                return encoding.GetBytes("--" + contentType.Substring(num1 + 9));
            }
            return null;
        }
        #endregion
        public void Init(HttpApplication context)
        {
            //      context.AddOnBeginRequestAsync(new BeginEventHandler(this.@ӳ), new EndEventHandler(this.@Ӵ));
            context.BeginRequest += Context_BeginRequest;
            context.AcquireRequestState += Context_AcquireRequestState;
            context.EndRequest += Context_EndRequest;
            context.Error += Context_Error;
        }
        private void Context_BeginRequest(object sender, EventArgs e)
        {
            HttpContext contex = (HttpContext)sender;
            contex.Items[typeof(MulitUploadFilModule)] = this;
            try
            {
                var application = sender as HttpApplication;
                var request = (HttpWorkerRequest)((IServiceProvider)application).GetService(typeof(HttpWorkerRequest));
                string contentType = application.Request.ContentType;
                if (contentType.IndexOf("multipart/form-data") <= -1)
                {
                    return;
                }
                Encoding encoding = application.Context.Request.ContentEncoding;
                byte[] boundary = this.ExtractBoundary(contentType, encoding);
                int bufsize = 48*1024;
                long length = 0;
                long position = 0;
            }
            catch (Exception)
            {

                throw;
            }
        }


        private void Context_Error(object sender, EventArgs e)
        {
            HttpContext contex = (HttpContext)sender;
            Error(contex);
        }

        private void Context_EndRequest(object sender, EventArgs e)
        {
            HttpContext contex = (HttpContext)sender;
            EndRequest(contex);
        }

        private void Context_AcquireRequestState(object sender, EventArgs e)
        {
            if (IsCheckLogin != "0")
            {
                HttpContext contex = (HttpContext)sender;
                if (contex.Request.HttpMethod != "POST")
                {
                    return;
                }
                string uploadParam = GetUploadParam(contex, "UseUploadModule");
                if (uploadParam == null)
                {
                    return;
                }
                if (GetUploadParam(contex, "_Namespace") != "CuteWebUI")
                {
                    return;
                }
                if (contex.Request.ContentLength == 0)
                {
                    string text = contex.Request.Headers["Authorization"];
                    if (text != null && text.StartsWith("NTLM "))
                    {
                        contex.Response.Write("Error:" + JSEncode("Unable upload data via Flash+NTLM"));
                        MulitUploadFilModule.EndRequest(contex);
                        return;
                    }
                }
                contex.Response.Buffer = true;
                contex.Response.Cache.SetAllowResponseInBrowserHistory(false);
                string uploadParam2 = GetUploadParam(contex, "_Addon");
                if (!string.IsNullOrEmpty(uploadParam2))
                {
                    if (uploadParam2 == "upload")
                    {
                        Guid guid = new Guid(GetUploadParam(contex, "_AddonGuid"));
                        string text2 = (string)contex.Cache["UploaderError:" + guid.ToString()];
                        if (GetUploadParam(contex, "GetUploaderError") == "1")
                        {
                            if (text2 == null)
                            {
                                contex.Response.StatusCode = 404;
                                contex.Response.Write("Error Not Found");
                                MulitUploadFilModule.EndRequest(contex);
                            }
                            else
                            {
                                contex.Response.ContentType = "text/plain";
                                contex.Response.Write(text2);
                                MulitUploadFilModule.EndRequest(contex);
                            }
                        }
                        else
                        {
                            MulitUploadFilModule.EndRequest(contex);
                        }
                    }
                    //if (uploadParam2 == "xhttp")
                    //{
                    //    UploadModule.HandleAddonUpload(contex);
                    //    MulitUploadFilModule.EndRequest(contex);
                    //}
                    //if (uploadParam2 == "verify")
                    //{
                    //    UploadModule.@Ө(contex);
                    //}
                    //return;
                }
                //if (GetUploadParam(contex, "CheckUploadStatus") == "True")
                //{
                //    UploadModule.@ӫ(contex);
                //    return;
                //}
                //contex.Items["IsIFrameUploadRequest"] = true;
                //if (uploadParam == "Dynamic")
                //{
                //    this.@Ӫ(contex);
                //}
                //UploadModule.@Ө(contex);
            }
        }



        #region static
        private static char[] illegitmacyChar = "<>".ToCharArray();
        /// <summary>
        /// 
        /// </summary>
        static string IsCheckLogin = System.Configuration.ConfigurationManager.AppSettings["IsCheckLogin"];

        // Token: 0x06000068 RID: 104 RVA: 0x0000401E File Offset: 0x0000221E
        public static string JSEncode(string str)
        {
            if (str == null)
            {
                return "";
            }
            return new Regex("\\\\|\\\"|\\r|\\n|\\'|\\<|\\>|\\&", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline).Replace(str, new MatchEvaluator(JSilleg));
        }

        // Token: 0x06000069 RID: 105 RVA: 0x00004048 File Offset: 0x00002248
        private static string JSilleg(Match @ӓ)
        {
            char c = @ӓ.Value[0];
            string text = "0123456789ABCDEF";
            int index = (int)(c & '\u000f');
            int index2 = (int)((c & 'ð') / '\u0010');
            return "\\x" + text[index2].ToString() + text[index].ToString();
        }
        public static string GetFileName(string filepath)
        {
            int num = filepath.LastIndexOfAny(new char[]
            {
            '/',
            '\\'
            });
            if (num != -1)
            {
                filepath = filepath.Substring(num + 1);
            }
            char[] array = new char[]
            {
            '<',
            '>',
            '|',
            ':',
            '"',
            '*',
            '?'
            };
            char[] InvalidPathChars = Path.GetInvalidFileNameChars();
            char[] array2 = new char[InvalidPathChars.Length + array.Length];
            Array.Copy(InvalidPathChars, 0, array2, 0, InvalidPathChars.Length);
            Array.Copy(array, 0, array2, InvalidPathChars.Length, array.Length);
            if (filepath.IndexOfAny(array2) == -1)
            {
                return filepath;
            }
            char[] array3 = filepath.ToCharArray();
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char c in array3)
            {
                if (Array.IndexOf<char>(array2, c) != -1)
                {
                    StringBuilder stringBuilder2 = stringBuilder.Append("_x");
                    int num2 = (int)c;
                    stringBuilder2.Append(num2.ToString("X")).Append("_");
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString();
        }
        public static string GetUploadParam(HttpContext context, string name)
        {
            NameValueCollection nameValueCollection = (NameValueCollection)context.Items["CuteWebUI.AjaxUploader.QueryString"];
            string text = (nameValueCollection != null) ? nameValueCollection[name] : context.Request.QueryString[name];
            if (text == null)
            {
                string text2 = context.Request.ServerVariables["HTTP_X_REWRITE_URL"];
                if (!string.IsNullOrEmpty(text2))
                {
                    int num = text2.IndexOf('/');
                    if (num != -1)
                    {
                        string[] array = text2.Substring(num + 1).Split(new char[]
                        {
                        '&'
                        });
                        string text3 = name + "=";
                        foreach (string text4 in array)
                        {
                            if (text4.StartsWith(text3.ToLower()))
                            {
                                text = text4.Substring(text3.Length);
                                text = HttpUtility.UrlDecode(text);
                            }
                        }
                    }
                }
            }
            if (text == null)
            {
                return null;
            }
            if (text.IndexOfAny(illegitmacyChar) != -1)
            {
                return "Invalid-" + name;
            }
            return text;
        }

        // Token: 0x0600005B RID: 91 RVA: 0x00003B38 File Offset: 0x00001D38
        public static string FindBoundary(HttpContext context)
        {
            string text = null;
            string contentType = context.Request.ContentType;
            int num = contentType.IndexOf("boundary=");
            if (num != -1)
            {
                int num2 = contentType.IndexOf(";", num);
                if (num2 == -1)
                {
                    text = contentType.Substring(num + 9);
                }
                else
                {
                    text = contentType.Substring(num + 9, num2 - num - 9);
                }
                text = text.Trim().Trim(new char[]
                {
                '"',
                '\''
                });
            }
            if (text == null)
            {
                return null;
            }
            return "--" + text;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="contex"></param>
        private static void EndRequest(HttpContext contex)
        {
            try
            {
                contex.ApplicationInstance.CompleteRequest();
            }
            catch (Exception)
            {
                contex.Response.End();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        private static void Error(HttpContext context)
        {
            Exception ex = context.Error;
            while (ex is HttpException && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            context.Response.Write(ex.ToString());
            if (true.Equals(context.Items["IsIFrameUploadRequest"]))
            {
                try
                {
                    context.Response.Clear();
                }
                catch
                {
                }
                //    UploadModule.@Ӣ(contex, ex, null);
                try
                {
                    context.Response.Flush();
                }
                catch
                {
                }
                context.ApplicationInstance.CompleteRequest();
                context.Response.End();
                return;
            }
            if (context.Request.Headers["CuteWebUI.AjaxUploader.Silverlight"] == "1")
            {
                Guid guid = Guid.NewGuid();// new Guid(GetUploadParam(context, "_AddonGuid"));
                context.Cache["UploaderError:" + guid.ToString()] = ex.ToString();
                try
                {
                    context.Response.Clear();
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/plain";
                }
                catch (Exception ex2)
                {
                    ex2.ToString();
                }
                context.Response.Write("Error:" + ex.ToString());
                try
                {
                    context.Response.Flush();
                }
                catch
                {
                }
                context.ApplicationInstance.CompleteRequest();
                context.Response.End();
                return;
            }
            EndRequest(context);
        }
        #endregion
    }
    //internal class ProcessUI
    //{
    //    // Token: 0x040000E6 RID: 230
    //    public DateTime Start = DateTime.Now;

    //    // Token: 0x040000E7 RID: 231
    //    public int ContentLength;

    //    // Token: 0x040000E8 RID: 232
    //    public int UploadedLength;

    //    // Token: 0x040000E9 RID: 233
    //    public bool Finish;

    //    // Token: 0x040000EA RID: 234
    //    public bool Disconnect;

    //    // Token: 0x040000EB RID: 235
    //    public bool Intercept;

    //    // Token: 0x040000EC RID: 236
    //    public string Error;
    //}
    //internal class HttpWorkerRequestStatic
    //{
    //    // Token: 0x06000328 RID: 808 RVA: 0x00010943 File Offset: 0x0000EB43
    //    public static HttpWorkerRequest New(HttpWorkerRequest wr, ProcessUI us)
    //    {
    //        return new @ӻ(wr, us);
    //    }

    //    // Token: 0x06000329 RID: 809 RVA: 0x0001094C File Offset: 0x0000EB4C
    //    public static void ChangeToStreamModeIfNeeded(HttpWorkerRequest wr, HttpContext context)
    //    {
    //        ((@ӻ)wr).ChangeToStreamModeIfNeeded(context);
    //    }

    //    // Token: 0x0600032A RID: 810 RVA: 0x0001095A File Offset: 0x0000EB5A
    //    public static bool IsStreamMode(HttpWorkerRequest wr)
    //    {
    //        return ((@ӻ)wr).IsStreamMode;
    //    }

    //    // Token: 0x0600032B RID: 811 RVA: 0x00010967 File Offset: 0x0000EB67
    //    public static bool IsReadEntityBodyCalled(HttpWorkerRequest wr)
    //    {
    //        return ((@ӻ)wr).IsReadEntityBodyCalled;

    //    } 
}