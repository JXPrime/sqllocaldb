﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.0
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace System.Data.SqlLocalDb {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Strings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Strings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("System.Data.SqlLocalDb.Strings", typeof(Strings).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} Test Application
        ///{1}
        ///
        ///Operating System:               {2}
        ///Is 64-bit Operating System?     {3}
        ///Is 64-bit Process?              {4}
        ///Image Runtime Version:          {5}
        ///Assembly Version:               {6}
        ///Assembly File Version:          {7}
        ///Assembly Informational Version: {8}
        ///Assembly Configuration:         {9}
        ///.
        /// </summary>
        internal static string Program_BannerFormat {
            get {
                return ResourceManager.GetString("Program_BannerFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Press any key to exit....
        /// </summary>
        internal static string Program_ExitPrompt {
            get {
                return ResourceManager.GetString("Program_ExitPrompt", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to LocalDB Instances:.
        /// </summary>
        internal static string Program_InstancesListHeader {
            get {
                return ResourceManager.GetString("Program_InstancesListHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Installed LocalDB Versions:.
        /// </summary>
        internal static string Program_VersionsListHeader {
            get {
                return ResourceManager.GetString("Program_VersionsListHeader", resourceCulture);
            }
        }
    }
}
