using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace AKCardScanner
{
    /// <summary>
    /// Compares the CardData XML files currently on disk against the SHA256
    /// hashes recorded when this version was built (CardData\checksums.json)
    /// to detect whether the user has hand-edited any of them. Used to warn
    /// before an update, since the installer overwrites these files.
    /// </summary>
    public static class CardDataIntegrityChecker
    {
        private const string ChecksumFileName = "checksums.json";

        /// <summary>Returns the file names (e.g. "AnimalCards.xml") that differ from their recorded checksum.</summary>
        public static List<string> GetModifiedFiles(string cardDataDir = null)
        {
            cardDataDir ??= Path.Combine(AppContext.BaseDirectory, "CardData");
            var modified = new List<string>();

            string checksumPath = Path.Combine(cardDataDir, ChecksumFileName);
            if (!File.Exists(checksumPath))
            {
                // No baseline to compare against (e.g. an install that predates
                // this feature) - nothing we can meaningfully detect, so don't warn.
                return modified;
            }

            Dictionary<string, string> expectedHashes;
            try
            {
                expectedHashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(checksumPath))
                                  ?? new Dictionary<string, string>();
            }
            catch
            {
                // Corrupt checksum file - treat as "nothing to compare" rather
                // than false-alarming the user.
                return modified;
            }

            foreach (var entry in expectedHashes)
            {
                string filePath = Path.Combine(cardDataDir, entry.Key);
                if (!File.Exists(filePath))
                {
                    modified.Add(entry.Key); // missing counts as modified
                    continue;
                }

                string actualHash = ComputeSha256(filePath);
                if (!string.Equals(actualHash, entry.Value, StringComparison.OrdinalIgnoreCase))
                {
                    modified.Add(entry.Key);
                }
            }

            return modified;
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
    }
}