// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Collections.Generic;

using SiliconStudio.Core.IO;

namespace SiliconStudio.Assets.Analysis
{
    /// <summary>
    /// Helper to find available new asset locations and identifiers.
    /// </summary>
    public sealed class AssetResolver
    {
        /// <summary>
        /// Delegate to test if the specified location is already used.
        /// </summary>
        /// <param name="location">The location to try to use.</param>
        /// <returns><c>true</c> if the specified location is already used, <c>false</c> otherwise.</returns>
        public delegate bool ContainsAssetWithLocationDelegate(UFile location);

        /// <summary>
        /// Delegate to test if an asset id is already used.
        /// </summary>
        /// <param name="guid">The unique identifier.</param>
        /// <returns><c>true</c> if an asset id is already used, <c>false</c> otherwise.</returns>
        public delegate bool ContainsAssetWithIdDelegate(Guid guid);

        private readonly HashSet<Guid> existingIds;
        private readonly HashSet<string> existingLocations;

        private readonly ContainsAssetWithLocationDelegate containsLocation;

        private readonly ContainsAssetWithIdDelegate containsId;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetResolver"/> class.
        /// </summary>
        public AssetResolver() : this(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetResolver" /> class.
        /// </summary>
        /// <param name="containsAssetWithLocation">The delegate used to check if an asset location is already used.</param>
        /// <param name="containsAssetWithId">The delegate used to check if an asset identifier is already used.</param>
        public AssetResolver(ContainsAssetWithLocationDelegate containsAssetWithLocation, ContainsAssetWithIdDelegate containsAssetWithId)
        {
            existingIds = new HashSet<Guid>();
            existingLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ContainsAssetWithLocation = containsAssetWithLocation;
            ContainsAssetWithId = containsAssetWithId;
            containsLocation = DefaultContainsLocation;
            containsId = DefaultContainsId;
        }

        /// <summary>
        /// Gets the locations already used.
        /// </summary>
        /// <value>The locations.</value>
        public HashSet<string> ExistingLocations
        {
            get
            {
                return existingLocations;
            }
        }

        /// <summary>
        /// Gets the asset ids already used.
        /// </summary>
        /// <value>The existing ids.</value>
        public HashSet<Guid> ExistingIds
        {
            get
            {
                return existingIds;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to always generate a new id on <see cref="RegisterId"/>.
        /// </summary>
        /// <value><c>true</c> if [force new identifier]; otherwise, <c>false</c>.</value>
        public bool AlwaysCreateNewId { get; set; }

        /// <summary>
        /// Gets or sets a delegate to test if a location is already used.
        /// </summary>
        /// <value>A delegate to test if a location is already used.</value>
        public ContainsAssetWithLocationDelegate ContainsAssetWithLocation { get; set; }

        /// <summary>
        /// Gets or sets a delegate to test if an asset id is already used.
        /// </summary>
        /// <value>A delegate to test if an asset id is already used.</value>
        public ContainsAssetWithIdDelegate ContainsAssetWithId { get; set; }

        /// <summary>
        /// Finds a name available for a new asset. This method will try to create a name based on an existing name and will append
        /// "_" + (number++) on every try. The new location found is added to the known existing locations.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="newLocation">The new location.</param>
        /// <returns><c>true</c> if there is a new location, <c>false</c> otherwise.</returns>
        public bool RegisterLocation(UFile location, out UFile newLocation)
        {
            var result = FindAvailableLocation(location, containsLocation, out newLocation);
            ExistingLocations.Add(newLocation ?? location);
            return result;
        }

        /// <summary>
        /// Registers an asset identifier for usage.
        /// </summary>
        /// <param name="assetId">The asset identifier.</param>
        /// <param name="newGuid">The new unique identifier if an asset has already been registered with the same id.</param>
        /// <returns><c>true</c> if the asset id is already in used. <paramref name="newGuid" /> contains a new guid, <c>false</c> otherwise.</returns>
        public bool RegisterId(Guid assetId, out Guid newGuid)
        {
            newGuid = assetId;
            var result = AlwaysCreateNewId || containsId(assetId);
            if (result)
            {
                newGuid = Guid.NewGuid();
            }
            ExistingIds.Add(newGuid);
            return result;
        }

        /// <summary>
        /// Creates a new <see cref="AssetResolver" /> using an existing package to check the existence of asset locations and ids.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <returns>A new AssetResolver.</returns>
        /// <exception cref="System.ArgumentNullException">package</exception>
        public static AssetResolver FromPackage(Package package)
        {
            if (package == null) throw new ArgumentNullException("package");

            var packagesForLocation = package.FindDependencies(true);
            var packagesForIds = package.Session != null ? package.Session.Packages : packagesForLocation;

            return new AssetResolver(packagesForLocation.ContainsAsset, packagesForIds.ContainsAsset);
        }

        /// <summary>
        /// Creates a new <see cref="AssetResolver"/> using an existing package to check the existence of asset locations and ids.
        /// </summary>
        /// <param name="packages">The packages.</param>
        /// <returns>A new AssetResolver.</returns>
        /// <exception cref="System.ArgumentNullException">package</exception>
        public static AssetResolver FromPackage(IList<Package> packages)
        {
            if (packages == null) throw new ArgumentNullException("packages");
            return new AssetResolver(packages.ContainsAsset, packages.ContainsAsset);
        }

        /// <summary>
        /// Default delegate for location.
        /// </summary>
        private bool DefaultContainsLocation(UFile location)
        {
            if (ExistingLocations.Contains(location))
            {
                return true;
            }
            if (ContainsAssetWithLocation != null)
            {
                return ContainsAssetWithLocation(location);
            }
            return false;
        }

        /// <summary>
        /// Default delegate for ids.
        /// </summary>
        private bool DefaultContainsId(Guid guid)
        {
            if (ExistingIds.Contains(guid))
            {
                return true;
            }
            if (ContainsAssetWithId != null)
            {
                return ContainsAssetWithId(guid);
            }
            return false;
        }

        /// <summary>
        /// Finds a name available for a new asset. This method will try to create a name based on an existing name and will append
        /// "_" + (number++) on every try.
        /// </summary>
        /// <param name="location">The path of the original asset.</param>
        /// <param name="containsAssetWithLocation">The try location.</param>
        /// <param name="newLocation">A new asset location that has the guaranty to be available when adding the assets to a package..</param>
        /// <returns><c>true</c> if this is a new location, false otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">localNames</exception>
        private static bool FindAvailableLocation(UFile location, ContainsAssetWithLocationDelegate containsAssetWithLocation, out UFile newLocation)
        {
            if (location == null) throw new ArgumentNullException("location");
            if (containsAssetWithLocation == null) throw new ArgumentNullException("containsAssetWithLocation");

            var pathStr = location.FullPath;
            int i = 1;
            string newPath = pathStr;
            while (containsAssetWithLocation(newPath))
            {
                newPath = pathStr + "_" + i;
                i++;
            }

            newLocation = newPath;
            return i > 1;
        }
    }
}