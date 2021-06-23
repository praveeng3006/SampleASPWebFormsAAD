using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;

namespace SampleWebFormsAAD
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
   //******************NOTE: Correct/update the value of key  "ida:Domain" in Web.config file before RUN this application********************************
            ConfigureAuth(app);
        }
    }
}
