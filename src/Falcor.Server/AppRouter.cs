using System;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;

namespace Falcor.Server
{
    public class AppRouter : FalcorRouter<HttpRequest>
    {
        public AppRouter()
        {
            Get["howdy.hello"] = _ =>
            {
                return null;
            };
        }
    }
}