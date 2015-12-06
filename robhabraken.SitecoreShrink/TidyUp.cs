﻿
namespace robhabraken.SitecoreShrink
{
    using Sitecore;
    using Sitecore.Configuration;
    using Sitecore.Data.Items;
    using Sitecore.Data.Archiving;
    using Sitecore.SecurityModel;
    using Sitecore.Resources.Media;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Sitecore.Data;

    public class TidyUp
    {
        //TODO: auto publish after clean up? Or should I make this optional?
        //TODO: test recycle and delete with multi language items
        //TODO: decide if security disabler is a good choice, or should we arrange security on page level

        // advise to run orphan clean up after deleting items but before recycling, also warn that orphan method invalidates recycled items (or could do so)

        private Database database;

        public TidyUp(string databaseName)
        {
            this.database = Factory.GetDatabase(databaseName);
        }

        /// <summary>
        /// Saves the media files of the given items to disk, using the folder structure of the media library.
        /// </summary>
        /// <param name="items">A list of items to download.</param>
        /// <param name="targetPath">The target location for the items to be downloaded to.</param>
        public void Download(List<Item> items, string targetPath)
        {
            foreach (var item in items)
            {
                var mediaItem = (MediaItem)item;
                var media = MediaManager.GetMedia(mediaItem);
                var stream = media.GetStream();

                var fullPath = this.MediaToFilePath(targetPath, mediaItem.MediaPath, mediaItem.Extension);

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                using (var targetStream = File.OpenWrite(fullPath))
                {
                    stream.CopyTo(targetStream);
                    targetStream.Flush();
                }
            }
        }

        /// <summary>
        /// Returns the file path to be used by the OS that corresponds to the location in the media library, starting at the given target path.
        /// </summary>
        /// <param name="targetPath">The starting path to store the media item in.</param>
        /// <param name="mediaPath">The Sitecore media path of the media item.</param>
        /// <param name="extension">The extensions of the Sitecore media item.</param>
        /// <returns></returns>
        private string MediaToFilePath(string targetPath, string mediaPath, string extension)
        {
            if(mediaPath.StartsWith("/"))
            {
                mediaPath = mediaPath.Substring(1);
            }
            
            return Path.Combine(targetPath, string.Format("{0}.{1}", mediaPath, extension));
        }

        public void Archive(List<Item> items) //TODO: test!!!!!!!!!!!!!!!
        {
            var archive = ArchiveManager.GetArchive("archive", database);

            if (archive != null)
            {
                foreach (var item in items) // check for children first?
                {
                    archive.ArchiveItem(item);
                }
            }
        }

        /// <summary>
        /// Deletes items by moving them to the recycle bin.
        /// </summary>
        /// <remarks>
        /// This applies to all versions and all languages of these items.
        /// </remarks>
        /// <param name="items">A list of items to recycle.</param>
        public void Recycle(List<Item> items)
        {
            using (new SecurityDisabler())
            {
                foreach (var item in items) // check for children first? or recycle children first
                {
                    item.Recycle(); 
                }
            }            
        }

        /// <summary>
        /// Deletes items permanently, bypassing the recycle bin.
        /// </summary>
        /// <remarks>
        /// This applies to all versions and all languages of these items.
        /// </remarks>
        /// <param name="items">A list of items to delete.</param>
        public void Delete(List<Item> items)
        {
            using (new SecurityDisabler())
            {
                foreach (var item in items) // check for children first? or delete children first
                {
                    item.Delete();
                }
            }
        }


        /// <summary>
        /// Deletes all versions of all languages except the latest version of each language and the current valid version of that language.
        /// </summary>
        /// <remarks>
        /// For most items the latest version of an item will be the current valid version as well, but when cleaning up old versions,
        /// you wouldn't want to delete a valid version if you are working on a newer but not yet publishable version.
        /// 
        /// Note that publishing target settings are shared across both versions and languages, so an item is either publishable to a specific target, or not.
        /// Getting the valid version can be done without consulting each separate publishing target, because if there _is_ a valid version for one of more targets,
        /// we do not want to delete it, and if there isn't a valid version _at all_ we can ignore the publishing settings and delete all versions but the last.
        /// </remarks>
        /// <param name="items">A list of items to delete the old versions of.</param>
        public void DeleteOldVersions(List<Item> items)
        {
            using (new SecurityDisabler())
            {
                foreach (var item in items)
                {
                    foreach (var language in item.Languages)
                    {
                        var languageItem = database.GetItem(item.ID, language);
                        var validVersion = languageItem.Publishing.GetValidVersion(DateTime.Now, true, false);
                        
                        foreach(var version in languageItem.Versions.GetVersions())
                        {
                            // delete everything but the latest version and the current valid version for this language
                            if(!version.Versions.IsLatestVersion() && version.Version.Number != validVersion.Version.Number)
                            {
                                version.Versions.RemoveVersion();
                            }
                        }
                    }
                }
            }
        }

        public void CleanUpOrphanedBlobs()
        {
            throw new NotImplementedException();
        }
    }
}
