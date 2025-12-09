using System.Security.Cryptography;
using System.Text;

namespace KimchiHedge.Core.Security;

/// <summary>
/// AES-256 암호화 서비스
/// </summary>
public class AesEncryptionService
{
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int SaltSize = 32;
    private const int Iterations = 100000;

    /// <summary>
    /// 문자열 암호화
    /// </summary>
    /// <param name="plainText">평문</param>
    /// <param name="password">암호화 키</param>
    /// <returns>Base64 인코딩된 암호문</returns>
    public string Encrypt(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] iv = RandomNumberGenerator.GetBytes(BlockSize / 8);

        using var key = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key.GetBytes(KeySize / 8);
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // 형식: salt (32) + iv (16) + ciphertext
        byte[] result = new byte[salt.Length + iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(iv, 0, result, salt.Length, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, salt.Length + iv.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 문자열 복호화
    /// </summary>
    /// <param name="cipherText">Base64 인코딩된 암호문</param>
    /// <param name="password">암호화 키</param>
    /// <returns>복호화된 평문</returns>
    public string Decrypt(string cipherText, string password)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentNullException(nameof(cipherText));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password));

        byte[] fullCipher = Convert.FromBase64String(cipherText);

        // salt, iv, ciphertext 분리
        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[BlockSize / 8];
        byte[] cipher = new byte[fullCipher.Length - SaltSize - iv.Length];

        Buffer.BlockCopy(fullCipher, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(fullCipher, SaltSize, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, SaltSize + iv.Length, cipher, 0, cipher.Length);

        using var key = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key.GetBytes(KeySize / 8);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// 바이트 배열 암호화
    /// </summary>
    public byte[] EncryptBytes(byte[] data, string password)
    {
        string base64 = Encrypt(Convert.ToBase64String(data), password);
        return Convert.FromBase64String(base64);
    }

    /// <summary>
    /// 바이트 배열 복호화
    /// </summary>
    public byte[] DecryptBytes(byte[] data, string password)
    {
        string decrypted = Decrypt(Convert.ToBase64String(data), password);
        return Convert.FromBase64String(decrypted);
    }
}
