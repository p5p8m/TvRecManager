//***********************************************************************************************************
// Revision       $Revision: 26014 $
// Last Modified  $Date: 2015-06-01 16:55:35 +0200 (Mo, 01. Jun 2015) $
// Author         $Author: pascal.melix $
// File           $URL: https://csvnhou-pro.houston.hp.com:18490/svn/sa_paf-tsrd/storage/source/trunk/sanxpert/Code/gui/sanreporter/AttributeReportGenerator.cs $
//***********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TvRecManager
{
    public static class FileOperations
    {
        /// <summary>
        /// Static method CopyDirectory() copies directory tree from source to destination.
        /// </summary>
        /// <param name="strSrcDir">Directory to coyp from</param>
        /// <param name="strDestDir">Destination directory for the tree</param>
        /// <param name="excludedDirectSubDirs">Direct subdirs not to be copied, case sensitive,
        /// no regexp or wild cards supported!!!</param>
        /// <returns>True if copy was successfull, false else</returns>
        public static bool CopyDirectory(string strSrcDir, string strDestDir, List<string> excludedDirectSubDirs, CancellationToken ct)
        {
            
            bool retVal = false;
            try
            {
                // ensure that source directory exists
                if (Directory.Exists(strSrcDir))
                {

                    if (!Directory.Exists(strDestDir))
                    {
                        Directory.CreateDirectory(strDestDir);
                    }
                    // ensure that destination directory exists
                    if (Directory.Exists(strDestDir))
                    {
                        String[] files = Directory.GetFileSystemEntries(strSrcDir);
                        string destName;
                        string destDirName;
                        foreach (string strEntry in files)
                        {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                            destDirName = Path.GetFileName(strEntry);
                            destName = Path.Combine(strDestDir, destDirName);
                            if (Directory.Exists(strEntry))
                            {
                                if (excludedDirectSubDirs == null || !excludedDirectSubDirs.Contains(destDirName))
                                {
                                    // If it a Sub directory
                                    if (!Directory.Exists(destName))
                                    {
                                        Directory.CreateDirectory(destName);
                                    }
                                    CopyDirectory(strEntry, destName, null, ct);
                                }
                            }
                            else
                            {
                                //  It is a file, Copy it in the "Documents and Settings" user temp directory
                                File.Copy(strEntry, destName, true);

                                //  Mark it as normal. This is made in order to avoid the problem to remove 
                                //  the temp directory containing read-only files.
                                File.SetAttributes(destName, FileAttributes.Normal);
                            }
                        }
                        retVal = !ct.IsCancellationRequested;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                throw;
            }
            return (retVal);
        }
        /// <summary>
        /// Static method CopyDirectory() copies directory tree from source to destination.
        /// </summary>
        /// <param name="strSrcDir">Directory to coyp from</param>
        /// <param name="strDestDir">Destination directory for the tree</param>
        /// <param name="excludedDirectSubDirs">Direct subdirs not to be copied, case sensitive,
        /// no regexp or wild cards supported!!!</param>
        /// <returns>True if copy was successfull, false else</returns>
        public static async Task<bool> CopyDirectoryAsync(string strSrcDir, string strDestDir, List<string> excludedDirectSubDirs, CancellationToken ct)
        {

            bool retVal = false;
            try
            {
                // ensure that source directory exists
                if (Directory.Exists(strSrcDir))
                {

                    if (!Directory.Exists(strDestDir))
                    {
                        Directory.CreateDirectory(strDestDir);
                    }
                    // ensure that destination directory exists
                    if (Directory.Exists(strDestDir))
                    {
                        String[] files = Directory.GetFileSystemEntries(strSrcDir);
                        string destName;
                        string destDirName;
                        foreach (string strEntry in files)
                        {
                            if (ct.IsCancellationRequested)
                            {
                                break;
                            }

                            destDirName = Path.GetFileName(strEntry);
                            destName = Path.Combine(strDestDir, destDirName);
                            if (Directory.Exists(strEntry))
                            {
                                if (excludedDirectSubDirs == null || !excludedDirectSubDirs.Contains(destDirName))
                                {
                                    // If it a Sub directory
                                    if (!Directory.Exists(destName))
                                    {
                                        Directory.CreateDirectory(destName);
                                    }
                                    retVal &= await CopyDirectoryAsync(strEntry, destName, null, ct);
                                }
                            }
                            else
                            {
                                //  It is a file, Copy it in the "Documents and Settings" user temp directory
                                await Task.Run( () => File.Copy(strEntry, destName, true), ct);

                                //  Mark it as normal. This is made in order to avoid the problem to remove 
                                //  the temp directory containing read-only files.
                                File.SetAttributes(destName, FileAttributes.Normal);
                            }
                        }
                        retVal = !ct.IsCancellationRequested;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                retVal = false;
            }
            return (retVal);
        }
        //private async Task<bool> FileCopyAsync(string filePathName, string destName, bool overwrite)
        //{
        //    bool retVal = false;
        //    try
        //    {
        //        await Task.Run(File.Copy(filePathName, destName, overwrite));
        //        retVal = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Trace.WriteLine(ex.Message);
        //        retVal = false;
        //    }
        //    return retVal;
        //}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="treeRoot"></param>
        /// <returns>true if really deleted, false else</returns>
        public static bool DeleteTree(string treeRoot, CancellationToken ct)
        {
            bool retVal = false;
            int numLoops = 0;
            while (!retVal && numLoops < 3) // Avoid never ending story
            {
                numLoops++;
                try
                {
                    if (Directory.Exists(treeRoot))
                    {
                        Directory.Delete(treeRoot, true);
                    }
                    retVal = true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Trace.WriteLine(
                        "Because of file protections could not delete at first try the subtree under: " + treeRoot +
                        ". Will continue on a smarter way to try. " + ex.Message);
                }
                catch (IOException ex)
                {
                    Trace.WriteLine(
                        "Could not delete at first try the subtree under: " + treeRoot +
                        ". This may be due to file or directiory attributes. Will continue on a smarter way to try. " + ex.Message);
                }
                // Other types of exception are propagated
                catch (Exception ex)
                {
                    Trace.WriteLine(
                        "Error trying to delete the subtree under: " + treeRoot + ". " + ex.Message);
                    throw;
                }
                if (!retVal)
                {
                    recSetAttrToNormal(treeRoot);
                }
                if (ct.IsCancellationRequested)
                {
                    retVal = false;
                    break;
                }
            }
            //if (!retVal)
            //{
            //    throw new Exception("Directory: " + treeRoot + " could not be destroyed. Giving up!");
            //}
            return (retVal);
        }
        public static string GetRelativePath(string path)
        {
            string retVal = path;
            if ( Path.IsPathRooted(retVal) )
            {
                string root = Path.GetPathRoot(retVal);
                retVal = retVal.Substring(root.Length);
            }
            return (retVal);
        }
        /// <summary>
        /// Recursively reset the read-only attributes in the whole subtree
        /// </summary>
        /// <param name="topDir">Root of sub tree</param>
        private static void recSetAttrToNormal(string topdirectory)
        {
            var di = new DirectoryInfo(topdirectory);
            var fileInfos = di.EnumerateFiles("*", SearchOption.AllDirectories);

            foreach (var fileInfo in fileInfos)
            {
                try
                {
                    fileInfo.Attributes = FileAttributes.Normal;
                }
                catch (System.Security.SecurityException ex)
                {
                    Trace.WriteLine(
                        "The caller does not have the " +
                        "required permission for the file: " + fileInfo.Name + ". Message: " + ex.Message);
                    throw;
                }
            }
        }
    }
}
