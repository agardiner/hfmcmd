using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;

using log4net;


namespace Encryption
{

    /// <summary>
    /// Encrypts data using Windows ProtectedData functionality. This does not
    /// require an encryption key, since the encryption/decryption is performed
    /// by Windows, and hence is a black-box. The downside of this approach is
    /// that the encrypted data can only be decrypted on the same physical
    /// server.
    /// </summary>
    public static class WindowsProtectedData
    {

        /// <summary>
        /// Encrypts a string using Windows built-in ProtectedData
        /// </summary>
        public static string Encrypt(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherTextBytes = ProtectedData.Protect(plainTextBytes, null,
                    DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(cipherTextBytes);
        }


        /// <summary>
        /// Decrypts a string encrypted using Windows built-in ProtectedData
        /// </summary>
        public static string Decrypt(string cipherText)
        {
            var cipherTextBytes = Convert.FromBase64String(cipherText);
            var plainTextBytes = ProtectedData.Unprotect(cipherTextBytes, null,
                    DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plainTextBytes, 0, plainTextBytes.Length);
        }

    }


    /// <summary>
    /// Encrypts data using teh 256-bit AES cipher (aka Rijndael).
    /// This cipher algorithm provides both portability and strong security of
    /// the encrypted data, provided the password is not stored on the same
    /// machine as the encryption key.
    /// </summary>
    public static class AES
    {
        // Reference to class logger
        private static readonly ILog _log = LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// Number of bits to use for encryption
        private static int KeySize = 256;

        /// Path to the encryption key file
        public static string EncryptionKeyFile
        {
            get {
                return Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".key");
            }
        }


        /// <summary>
        /// Encrypts a string using AES 256-bit encryption
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if(string.IsNullOrEmpty(plainText)) {
                return "";
            }

            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            var keyBytes = GetEncryptionKey();

            var aes = new RijndaelManaged();
            aes.KeySize = KeySize;
            aes.Mode = CipherMode.CBC;
            aes.GenerateIV();               // We use a generated IV

            var ivBytes = aes.IV;
            byte[] cipherTextBytes = null;

            using(var encryptor = aes.CreateEncryptor(keyBytes, ivBytes)) {
                using(var ms = new MemoryStream()) {
                    using(var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write)) {
                        // Prepend the IV to the encrypted bytes, so that we can
                        // retrieve it later when decrypting. (The IV is used to
                        // randomize the encryption process, so that the same password
                        // encrypted twice generates a different cipher text each time).
                        // As it is only used for randomisation on encryption, it does
                        // not matter that it can be extracted easily from the cipher
                        // text; without the encryption key, the IV is useless.
                        ms.Write(ivBytes, 0, aes.IV.Length);
                        cs.Write(plainTextBytes, 0, plainTextBytes.Length);
                        cs.FlushFinalBlock();
                        cipherTextBytes = ms.ToArray();
                        ms.Close();
                        cs.Close();
                    }
                }
            }
            aes.Clear();

            return Convert.ToBase64String(cipherTextBytes);
        }


         /// <summary>
         /// Decrypts a string previously encrypted with 256-bit AES
         /// </summary>
         public static string Decrypt(string cipherText)
         {
            if(string.IsNullOrEmpty(cipherText)) {
                return "";
            }

            var cipherTextBytes = Convert.FromBase64String(cipherText);
            var keyBytes = LoadEncryptionKey();
            if(keyBytes == null) {
                throw new Exception("No encryption key could be found");
            }

            var aes = new RijndaelManaged();
            aes.KeySize = KeySize;
            aes.Mode = CipherMode.CBC;

            int ivLen = aes.IV.Length;
            var plainTextBytes = new byte[cipherTextBytes.Length - ivLen];
            var ivBytes = new byte[ivLen];
            int byteCount = 0;

            using(var ms = new MemoryStream(cipherTextBytes)) {
                // Read the IV that was used to generate the cipher text
                ms.Read(ivBytes, 0, ivLen);
                using(var decryptor = aes.CreateDecryptor(keyBytes, ivBytes)) {
                    using(var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read)) {
                        byteCount = cs.Read(plainTextBytes, 0, plainTextBytes.Length);
                        ms.Close();
                        cs.Close();
                    }
                }
            }
            aes.Clear();

            return Encoding.UTF8.GetString(plainTextBytes, 0, byteCount);
        }


        // Retrieves or generates an encryption key
        private static byte[] GetEncryptionKey()
        {
            var keyBytes = LoadEncryptionKey();
            if(keyBytes == null) {
                keyBytes = GenerateEncryptionKey();
            }
            return keyBytes;
        }


        // Generates a new random byte array to use as the encryption key for
        // encryption and decryption. This is a much more secure approach than
        // embedding a "secret" password in the source code.
        private static byte[] GenerateEncryptionKey()
        {
            var aes = new RijndaelManaged();
            var keyBytes = aes.Key;
            var key = Convert.ToBase64String(keyBytes);
            SaveEncryptionKey(keyBytes);
            return keyBytes;
        }


        // Saves an encryption key to a file
        private static void SaveEncryptionKey(byte[] keyBytes)
        {
            var keyPath = EncryptionKeyFile;
            _log.InfoFormat("Saving encryption key to {0}", keyPath);

            using(var fs = new FileStream(keyPath, FileMode.Create)) {
                fs.Write(keyBytes, 0, keyBytes.Length);
                fs.Flush();
                fs.Close();
            }
            _log.Warn("An encryption key has just been generated for use in encrypting " +
                      "and decrypting passwords. This encryption key file is important, " +
                      "and you should consider whether you need to back it up. " +
                      "IF YOU LOSE THIS ENCRYPTION FILE, YOU WILL NOT BE ABLE TO DECRYPT " +
                      "ANY PASSWORDS ENCRYPTED WITH IT. You will however be able to create " +
                      "a new encryption key, and use it to generate new encrypted passwords.");
        }


        // Loads an encryption key from a file
        private static byte[] LoadEncryptionKey()
        {
            byte[] keyBytes = null;
            var keyPath = EncryptionKeyFile;
            if(File.Exists(keyPath)) {
                keyBytes = new byte[KeySize / 8];
                using(var fs = new FileStream(keyPath, FileMode.Open, FileAccess.Read)) {
                    fs.Read(keyBytes, 0, keyBytes.Length);
                    fs.Close();
                }
            }
            return keyBytes;
        }

    }

}

