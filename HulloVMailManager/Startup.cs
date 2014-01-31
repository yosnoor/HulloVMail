using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(HulloVMailManager.Startup))]
namespace HulloVMailManager
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
