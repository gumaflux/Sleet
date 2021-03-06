using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Sleet
{
    public static class SleetUtility
    {
        /// <summary>
        /// Add a package to all services.
        /// </summary>
        public static async Task AddPackage(SleetContext context, PackageInput package)
        {
            var services = GetServices(context);

            foreach (var service in services)
            {
                if (package.IsSymbolsPackage)
                {
                    var symbolsService = service as ISymbolsAddRemovePackages;

                    if (symbolsService != null)
                    {
                        await symbolsService.AddSymbolsPackageAsync(package);
                    }
                }
                else
                {
                    await service.AddPackageAsync(package);
                }
            }
        }

        /// <summary>
        /// Remove both the symbols and non-symbols package from all services.
        /// </summary>
        public static async Task RemovePackage(SleetContext context, PackageIdentity package)
        {
            await RemoveNonSymbolsPackage(context, package);
            await RemoveSymbolsPackage(context, package);
        }

        /// <summary>
        /// Remove a non-symbols package from all services.
        /// </summary>
        public static async Task RemoveNonSymbolsPackage(SleetContext context, PackageIdentity package)
        {
            var services = GetServices(context);

            foreach (var service in services)
            {
                await service.RemovePackageAsync(package);
            }
        }

        /// <summary>
        /// Remove a symbols package from all services.
        /// </summary>
        public static async Task RemoveSymbolsPackage(SleetContext context, PackageIdentity package)
        {
            var services = GetServices(context);
            var symbolsEnabled = context.SourceSettings.SymbolsEnabled;

            if (symbolsEnabled)
            {
                foreach (var symbolsService in services.Select(e => e as ISymbolsAddRemovePackages).Where(e => e != null))
                {
                    await symbolsService.RemoveSymbolsPackageAsync(package);
                }
            }
        }

        public static IReadOnlyList<ISleetService> GetServices(SleetContext context)
        {
            // Order is important here
            // Packages must be added to flat container, then the catalog, then registrations.
            var services = new List<ISleetService>
            {
                new FlatContainer(context)
            };

            if (context.SourceSettings.CatalogEnabled)
            {
                // Catalog on disk
                services.Add(new Catalog(context));
            }
            else
            {
                // In memory catalog
                services.Add(new VirtualCatalog(context));
            }

            services.Add(new Registrations(context));
            services.Add(new AutoComplete(context));
            services.Add(new Search(context));
            services.Add(new PackageIndex(context));

            // Symbols
            if (context.SourceSettings.SymbolsEnabled)
            {
                services.Add(new Symbols(context));
            }

            return services;
        }

        /// <summary>
        /// Pre-load files in parallel
        /// </summary>
        public static Task FetchFeed(SleetContext context)
        {
            return Task.WhenAll(GetServices(context).Select(e => e.FetchAsync()));
        }
    }
}