using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Routing;

namespace WebUploaderDemo.App_Start
{
    public class MvcMulitUploadFile_Hander : IHttpHandler
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {

            context.Response.Write("this is a Wheel for {0}Controller and {1}action ");
            context.Response.End();
        }
    }
    public class MvcMulitUploadFile_RouteHandler : System.Web.Routing.IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return new MvcMulitUploadFile_Hander();
        }


    }
    public class MvcMulitUploadFileModule : IHttpModule
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Init(HttpApplication context)
        {
            context.PostResolveRequestCache += new EventHandler(context_PostResolveRequestCache);
        }

        void context_PostResolveRequestCache(object sender, EventArgs e)
        {
            HttpContextBase context = new HttpContextWrapper(((HttpApplication)sender).Context);
            this.PostResolveRequestCache(context);

        }

        private void PostResolveRequestCache(HttpContextBase context)
        {
            RouteData routeData = RouteTable.Routes.GetRouteData(context);

            if (routeData == null)
            {
                throw new InvalidOperationException();
            }

            IRouteHandler routeHandler = routeData.RouteHandler;
            if (routeHandler == null)
            {
                throw new InvalidOperationException();
            }

            RequestContext requestContext = new RequestContext(context, routeData);
            IHttpHandler httpHandler = routeHandler.GetHttpHandler(requestContext);
            if (httpHandler == null)
            {
                throw new InvalidOperationException("无法创建对应的HttpHandler对象");
            }
            context.RemapHandler(httpHandler);

        }
    }
    public static class MvcMulitUploadFileExtension
    {
        public static Route MapMvcMulitUploadRoute(this RouteCollection routes, string name, string url, object defaults)
        {
            return MapMvcMulitUploadlRoute(routes, name, url, defaults, null, null);
        }

        public static Route MapMvcMulitUploadlRoute(this RouteCollection routes, string name, string url, object defaults, object constraints, string[] namespaces)
        {
            if (routes == null)
            {
                throw new ArgumentNullException("routes");
            }
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }
            // 在这里注册 Route 与 WheelRouteHandler的映射关系
            Route route = new Route(url, new MvcMulitUploadFile_RouteHandler())
            {
                Defaults = new RouteValueDictionary(defaults),
                Constraints = new RouteValueDictionary(constraints),
                DataTokens = new RouteValueDictionary()
            };

            if ((namespaces != null) && (namespaces.Length > 0))
            {
                route.DataTokens["Namespaces"] = namespaces;
            }

            routes.Add(name, route);

            return route;
        }

    }

}