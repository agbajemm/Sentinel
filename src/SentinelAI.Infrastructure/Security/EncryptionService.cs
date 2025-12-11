using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SentinelAI.Core.Exceptions;
using SentinelAI.Core.Interfaces;

namespace SentinelAI.Infrastructure.Security;

/// <summary>
/// Configuration for encryption service
/// </summary>
public class EncryptionSettings
{
    public const string SectionName = "Encryption";
    
    public required string MasterKey { get; set; }
    public required string Salt { get; set; }
    public int KeySize { get; set; } = 256;
    public int Iterations { get; set; } = 100000;
}

/// <summary>
/// AES-256 encryption service implementation
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly byte[] _masterKey;
    private readonly byte[] _salt;
    private readonly int _iterations;
    
    public EncryptionService(IOptions<EncryptionSettings> options)
    {
        var settings = options.Value;
        
        if (string.IsNullOrEmpty(settings.MasterKey) || settings.MasterKey.Length < 32)
        {
            throw new EncryptionException("Master encryption key must be at least 32 characters.");
        }
        
        _salt = Encoding.UTF8.GetBytes(settings.Salt);
        _iterations = settings.Iterations;
        
        // Derive key from master key using PBKDF2
        using var keyDerivation = new Rfc2898DeriveBytes(
            settings.MasterKey, 
            _salt, 
            _iterations, 
            HashAlgorithmName.SHA256);
        
        _masterKey = keyDerivation.GetBytes(settings.KeySize / 8);
    }
    
    /// <inheritdoc/>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;
        
        try
        {
            using var aes = Aes.Create();
            aes.Key = _masterKey;
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            // Combine IV and encrypted data
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
            
            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            throw new EncryptionException("Failed to encrypt data.", ex);
        }
    }
    
    /// <inheritdoc/>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;
        
        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            
            using var aes = Aes.Create();
            aes.Key = _masterKey;
            
            // Extract IV from the beginning
            var iv = new byte[aes.BlockSize / 8];
            var encryptedBytes = new byte[fullCipher.Length - iv.Length];
            
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, encryptedBytes, 0, encryptedBytes.Length);
            
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            throw new EncryptionException("Failed to decrypt data.", ex);
        }
    }
    
    /// <inheritdoc/>
    public string Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        using var sha256 = SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input + Convert.ToBase64String(_salt));
        var hashBytes = sha256.ComputeHash(inputBytes);
        
        return Convert.ToBase64String(hashBytes);
    }
    
    /// <inheritdoc/>
    public bool VerifyHash(string input, string hash)
    {
        var computedHash = Hash(input);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(hash));
    }
    
    /// <inheritdoc/>
    public string GenerateSecureKey(int length = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes);
    }
    
    /// <inheritdoc/>
    public string EncryptForTenant(string plainText, Guid tenantId)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;
        
        // Derive tenant-specific key
        var tenantKey = DeriveTenantKey(tenantId);
        
        try
        {
            using var aes = Aes.Create();
            aes.Key = tenantKey;
            aes.GenerateIV();
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);
            
            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            throw new EncryptionException("Failed to encrypt tenant data.", ex);
        }
    }
    
    /// <inheritdoc/>
    public string DecryptForTenant(string cipherText, Guid tenantId)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;
        
        var tenantKey = DeriveTenantKey(tenantId);
        
        try
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            
            using var aes = Aes.Create();
            aes.Key = tenantKey;
            
            var iv = new byte[aes.BlockSize / 8];
            var encryptedBytes = new byte[fullCipher.Length - iv.Length];
            
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, encryptedBytes, 0, encryptedBytes.Length);
            
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            throw new EncryptionException("Failed to decrypt tenant data.", ex);
        }
    }
    
    private byte[] DeriveTenantKey(Guid tenantId)
    {
        using var keyDerivation = new Rfc2898DeriveBytes(
            Convert.ToBase64String(_masterKey) + tenantId.ToString(),
            _salt,
            _iterations,
            HashAlgorithmName.SHA256);
        
        return keyDerivation.GetBytes(32);
    }
}
