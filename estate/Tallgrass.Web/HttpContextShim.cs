using Microsoft.AspNetCore.Http;

namespace Tallgrass.Web
{
    /// <summary>
    /// Added during the ASP.NET Core port so 140 call sites of HttpContext.Current
    /// could keep compiling. "Temporary." See the ratchet.
    /// </summary>
    public static class HttpContextShim
    {
        public static IHttpContextAccessor Accessor;
        public static HttpContext Current { get { return Accessor == null ? null : Accessor.HttpContext; } }
    }
}
