using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SearchService;
using UnityEngine;
using System.Reflection;
using UnityEditor.Search;

namespace Editor.Search {
    [SceneSearchEngine][InitializeOnLoad]
    public class AirshipComponentSearch : ISceneSearchEngine {
        private PropertyInfo searchFilterField;
        private MethodInfo setSearchFilterMethod;
        
        private string[] nameContains = new string[] {};
        private string airshipComponentSearch = "";

        static AirshipComponentSearch() {
            // Logic to set the Editor Preference default search engine to the
            // AirshipSceneSearchEngine (defined below)
            
            // Requires a delayCall because search engine isn't registered immediately on load
            EditorApplication.delayCall += () => {
                // SearchSettings.GetOrderedApis()
                var getOrderedApis = typeof(SearchSettings).GetMethod("GetOrderedApis",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var apis = (getOrderedApis.Invoke(null, new object[] { }) as IEnumerable<object>)?.ToArray();
                foreach (var api in apis) {
                    // orderedApi.SetActiveSearchEngine(searchEngines[index].name)
                    var searchApiType =
                        Assembly.Load("UnityEditor").GetType("UnityEditor.SearchService.ISearchApi");
                    var scope = searchApiType.GetProperty("engineScope").GetValue(api);
                    // Grab the scene 
                    if (Convert.ToInt32(scope) == (int) SearchEngineScope.Scene) {
                        searchApiType.GetMethod("SetActiveSearchEngine",
                            BindingFlags.Public | BindingFlags.Instance).Invoke(api, new object[] { "AirshipSceneSearchEngine", true });
                    }
                }
            };
        }
        
        public AirshipComponentSearch() {
            searchFilterField = typeof(SceneSearchContext).GetProperty("searchFilter", 
                BindingFlags.Instance | BindingFlags.NonPublic);
                
            // Get all methods named "SetSearchFilter" (there are multiple :>)
            var methods = typeof(HierarchyProperty).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            foreach (var method in methods) {
                if (method.Name != "SetSearchFilter") continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 1) {
                    setSearchFilterMethod = method;
                    break;
                }
            }
        }

        public bool Filter(ISearchContext context, string query, HierarchyProperty objectToFilter) {
            if (airshipComponentSearch != "") {
                var obj = objectToFilter.pptrValue;
                if (obj is GameObject go) {
                    var goName = go.name;
                    var acs = go.GetComponents<AirshipComponent>();
                    foreach (var ac in acs) {
                        if (ac.script.name.Equals(airshipComponentSearch, StringComparison.InvariantCultureIgnoreCase)) {
                            // We have the AC! Now, does it match the query name?
                            var containsAll = nameContains.All((contains) =>
                                goName.IndexOf(contains, StringComparison.CurrentCultureIgnoreCase) >= 0);
                            return containsAll;
                        }
                    }
                }
                return false;
            }

            // For all other queries, we return true as Unity's implementation does
            // The actual filtering happens in BeginSearch via SetSearchFilter
            return true;
        }

        public string name { get; } = "AirshipSceneSearchEngine";

        public void BeginSession(ISearchContext context) { }

        public void EndSession(ISearchContext context) {}

        public void BeginSearch(ISearchContext context, string query) {
            var airshipQuery = this.ParseAirshipQuery(query);
            if (airshipQuery) return;
            
            // Use reflection to access internal searchFilter and mimic Unity's behavior
            // stolen from Editor/Mono/Search/LegacyImplementations.cs
            if (context is SceneSearchContext sceneContext && 
                searchFilterField != null && 
                setSearchFilterMethod != null) {
                
                var searchFilter = searchFilterField.GetValue(sceneContext);
                var rootProperty = sceneContext.rootProperty;
                if (searchFilter != null && rootProperty != null) {
                    setSearchFilterMethod.Invoke(rootProperty, new[] { searchFilter });
                }
            }
        }

        public void EndSearch(ISearchContext context) {}

        private bool ParseAirshipQuery(string query) {
            // Clear search parameter
            airshipComponentSearch = "";
            
            var args = query.Split(" ");
            foreach (var arg in args) {
                if (arg.StartsWith("t:")) {
                    var componentName = arg.Split("t:")[1];
                    if (componentName.Length == 0) return false;
                    
                    // Stolen from CachedFilteredHierarchy.cs (Unity editor)
                    var type = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>()
                        .FirstOrDefault(t => componentName.Equals(t.FullName, StringComparison.InvariantCultureIgnoreCase) || componentName.Equals(t.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (type != default) return false;

                    airshipComponentSearch = componentName;
                    nameContains = args.Where(a => !a.Contains(":")).ToArray();
                    return true;
                }
            }
            return false;
        }
    }
}