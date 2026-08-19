//#define SKIN_OF_THE_EARTH_ONLY

using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.SystemCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.Noding;
using NetTopologySuite.Noding.Snapround;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Operation.Valid;
using NetTopologySuite.Simplify;
using S100FC.Topology;
using System.Data;
using System.Globalization;

namespace ArcGIS.Core.Data
{
    public static class Helper
    {
        public static Geodatabase OpenGeodatabase(string workspacePath, string portalUrl = "", string portalUser = "", string portalPassword = "", bool forceSignOut = true) {
            if (workspacePath.EndsWith(".geodatabase", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new MobileGeodatabaseConnectionPath(new Uri(workspacePath)));
            else if (workspacePath.EndsWith(".gdb", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspacePath)));
            else if (workspacePath.EndsWith(".sde", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new DatabaseConnectionFile(new Uri(workspacePath)));
            else if (workspacePath.StartsWith("memory", StringComparison.InvariantCultureIgnoreCase))
                return new Geodatabase(new MemoryConnectionProperties(workspacePath));
            else if (workspacePath.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
                return OpenFeatureService(workspacePath, portalUrl, portalUser, portalPassword, forceSignOut);
            else
                throw new ArgumentException("Unrecognized workspace type: " + workspacePath);
        }

        public static Geodatabase OpenFeatureService(string featureServiceUrl, string portalUrl, string portalUser, string portalPassword, bool forceSignOut = true) {
            var arcGisSignOn = ArcGISSignOn.Instance;
            var portalUri = new Uri(portalUrl);
            var workspaceUri = new Uri(featureServiceUrl);
            if (forceSignOut && arcGisSignOn.IsSignedOn(portalUri))
                arcGisSignOn.SignOut(portalUri);
            if (!arcGisSignOn.IsSignedOn(portalUri))
                arcGisSignOn.SignInWithCredentials(portalUri, portalUser, portalPassword, out var referer, out var token);

            return new Geodatabase(new ServiceConnectionProperties(workspaceUri));
        }
    }
}
