using System;
using System.Deployment.Application;
using System.Reflection;

namespace de.creinbold.FlatShare
{
    public class ProductVersion
    {
        public static Version Get()
        {
            try
            {
                return ApplicationDeployment.CurrentDeployment.CurrentVersion;
            }
            catch
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        public static string ToString(Version ver)
        {
            return string.Format("{0}.{1}.{2}", ver.Major, ver.Minor, ver.Revision);
        }
    }
}
