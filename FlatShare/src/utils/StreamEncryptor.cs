using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace de.creinbold.FlatShare
{
    public class StreamEncryptor
    {
        //  Call this function to remove the key from memory after use for security
        [DllImport("KERNEL32.DLL", EntryPoint = "RtlZeroMemory")]
        public static extern bool ZeroMemory(IntPtr Destination, int Length);

        unsafe public static bool ZeroMemory(byte[] array)
        {
            fixed (byte* p = array)
            {
                return ZeroMemory((IntPtr)p, array.Length * sizeof(byte));
            }
        }

        private static byte[] GenerateRandomSalt()
        {
            byte[] data = new byte[32];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                for (int i = 0; i < 10; i++)
                {
                    rng.GetBytes(data);
                }
            }

            return data;
        }

        private byte[] _HashedPassword;
        private byte[] _Cipher;
        private byte[] _Entropy = new byte[20];
        private byte[] _Password
        {
            get { return ProtectedData.Unprotect(_Cipher, _Entropy, DataProtectionScope.CurrentUser); }
            set { _Cipher = ProtectedData.Protect(value, _Entropy, DataProtectionScope.CurrentUser); }
        }

        public StreamEncryptor(byte[] password)
        {
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(_Entropy);
            }
            _Password = password;
            _HashedPassword = Hash(password);
            ZeroMemory(password);
        }

        private byte[] Hash(byte[] pw)
        {
            return new Rfc2898DeriveBytes(pw, _Entropy, 10000).GetBytes(32);
        }

        public bool Authentificate(byte[] pw)
        {
            var hash = Hash(pw);
            ZeroMemory(pw);
            if (hash.Length != _HashedPassword.Length) return false;

            for (int i = 0; i < hash.Length; i++)
            {
                if (hash[i] != _HashedPassword[i]) return false;
            }
            return true;
        }

        public void Encrypt(Stream sIn, Stream sOut)
        {
            //http://stackoverflow.com/questions/27645527/aes-encryption-on-large-files
            //generate random salt
            byte[] salt = GenerateRandomSalt();

            //Set Rijndael symmetric encryption algorithm
            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            AES.Padding = PaddingMode.PKCS7;

            //http://stackoverflow.com/questions/2659214/why-do-i-need-to-use-the-rfc2898derivebytes-class-in-net-instead-of-directly
            //"What it does is repeatedly hash the user password along with the salt." High iteration counts.
            var key = new Rfc2898DeriveBytes(_Password, salt, 10000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);

            //Cipher modes: http://security.stackexchange.com/questions/52665/which-is-the-best-cipher-mode-and-padding-mode-for-aes-encryption
            AES.Mode = CipherMode.CFB;

            // write salt to the begining of the output file, so in this case can be random every time
            sOut.Write(salt, 0, salt.Length);

            using (var cs = new CryptoStream(sOut, AES.CreateEncryptor(), CryptoStreamMode.Write))
            {
                sIn.CopyTo(cs);
            }
        }

        public void Decrypt(Stream sIn, Stream sOut)
        {
            byte[] salt = new byte[32];

            sIn.Read(salt, 0, salt.Length);

            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            var key = new Rfc2898DeriveBytes(_Password, salt, 10000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Padding = PaddingMode.PKCS7;
            AES.Mode = CipherMode.CFB;

            using (var cs = new CryptoStream(sIn, AES.CreateDecryptor(), CryptoStreamMode.Read))
            {
                cs.CopyTo(sOut);
            }
        }
    }
}
