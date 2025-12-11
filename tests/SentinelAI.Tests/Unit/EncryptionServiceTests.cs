using FluentAssertions;
using Microsoft.Extensions.Options;
using SentinelAI.Infrastructure.Security;
using Xunit;

namespace SentinelAI.Tests.Unit;

public class EncryptionServiceTests
{
    private readonly EncryptionService _encryptionService;
    
    public EncryptionServiceTests()
    {
        var settings = new EncryptionSettings
        {
            MasterKey = "TestMasterKeyThatIsAtLeast32CharactersLongForTesting!",
            Salt = "TestSaltForUnitTestingPurposes",
            KeySize = 256,
            Iterations = 10000
        };
        
        _encryptionService = new EncryptionService(Options.Create(settings));
    }
    
    [Fact]
    public void Encrypt_ShouldReturnEncryptedString()
    {
        // Arrange
        var plainText = "SensitiveAccountNumber123456";
        
        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        
        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plainText);
    }
    
    [Fact]
    public void Decrypt_ShouldReturnOriginalString()
    {
        // Arrange
        var plainText = "SensitiveAccountNumber123456";
        var encrypted = _encryptionService.Encrypt(plainText);
        
        // Act
        var decrypted = _encryptionService.Decrypt(encrypted);
        
        // Assert
        decrypted.Should().Be(plainText);
    }
    
    [Fact]
    public void Encrypt_ShouldProduceDifferentCiphertexts_ForSamePlaintext()
    {
        // Arrange
        var plainText = "SameTextToEncrypt";
        
        // Act
        var encrypted1 = _encryptionService.Encrypt(plainText);
        var encrypted2 = _encryptionService.Encrypt(plainText);
        
        // Assert (due to random IV, ciphertexts should differ)
        encrypted1.Should().NotBe(encrypted2);
    }
    
    [Fact]
    public void Encrypt_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var plainText = string.Empty;
        
        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        
        // Assert
        encrypted.Should().BeEmpty();
    }
    
    [Fact]
    public void Encrypt_WithNull_ShouldReturnNull()
    {
        // Arrange
        string? plainText = null;
        
        // Act
        var encrypted = _encryptionService.Encrypt(plainText!);
        
        // Assert
        encrypted.Should().BeNull();
    }
    
    [Fact]
    public void Hash_ShouldReturnConsistentHash()
    {
        // Arrange
        var input = "TestInput123";
        
        // Act
        var hash1 = _encryptionService.Hash(input);
        var hash2 = _encryptionService.Hash(input);
        
        // Assert
        hash1.Should().Be(hash2);
    }
    
    [Fact]
    public void Hash_ShouldReturnDifferentHash_ForDifferentInput()
    {
        // Arrange
        var input1 = "TestInput123";
        var input2 = "TestInput456";
        
        // Act
        var hash1 = _encryptionService.Hash(input1);
        var hash2 = _encryptionService.Hash(input2);
        
        // Assert
        hash1.Should().NotBe(hash2);
    }
    
    [Fact]
    public void VerifyHash_ShouldReturnTrue_ForMatchingHash()
    {
        // Arrange
        var input = "TestInput123";
        var hash = _encryptionService.Hash(input);
        
        // Act
        var result = _encryptionService.VerifyHash(input, hash);
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void VerifyHash_ShouldReturnFalse_ForNonMatchingHash()
    {
        // Arrange
        var input = "TestInput123";
        var wrongInput = "WrongInput";
        var hash = _encryptionService.Hash(input);
        
        // Act
        var result = _encryptionService.VerifyHash(wrongInput, hash);
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void GenerateSecureKey_ShouldReturnKeyOfSpecifiedLength()
    {
        // Arrange
        var length = 32;
        
        // Act
        var key = _encryptionService.GenerateSecureKey(length);
        
        // Assert
        key.Should().NotBeNullOrEmpty();
        // Base64 encoding increases size by ~4/3
        Convert.FromBase64String(key).Length.Should().Be(length);
    }
    
    [Fact]
    public void GenerateSecureKey_ShouldReturnUniqueKeys()
    {
        // Act
        var key1 = _encryptionService.GenerateSecureKey();
        var key2 = _encryptionService.GenerateSecureKey();
        
        // Assert
        key1.Should().NotBe(key2);
    }
    
    [Fact]
    public void EncryptForTenant_ShouldProduceTenantSpecificEncryption()
    {
        // Arrange
        var plainText = "TenantSpecificData";
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();
        
        // Act
        var encrypted1 = _encryptionService.EncryptForTenant(plainText, tenantId1);
        var encrypted2 = _encryptionService.EncryptForTenant(plainText, tenantId2);
        
        // Assert
        encrypted1.Should().NotBe(encrypted2);
    }
    
    [Fact]
    public void DecryptForTenant_ShouldDecryptWithCorrectTenant()
    {
        // Arrange
        var plainText = "TenantSpecificData";
        var tenantId = Guid.NewGuid();
        var encrypted = _encryptionService.EncryptForTenant(plainText, tenantId);
        
        // Act
        var decrypted = _encryptionService.DecryptForTenant(encrypted, tenantId);
        
        // Assert
        decrypted.Should().Be(plainText);
    }
    
    [Fact]
    public void DecryptForTenant_WithWrongTenant_ShouldThrowException()
    {
        // Arrange
        var plainText = "TenantSpecificData";
        var correctTenantId = Guid.NewGuid();
        var wrongTenantId = Guid.NewGuid();
        var encrypted = _encryptionService.EncryptForTenant(plainText, correctTenantId);
        
        // Act & Assert
        var action = () => _encryptionService.DecryptForTenant(encrypted, wrongTenantId);
        action.Should().Throw<Exception>();
    }
    
    [Theory]
    [InlineData("Short")]
    [InlineData("This is a medium length string for testing")]
    [InlineData("This is a much longer string that contains various special characters like !@#$%^&*() and numbers 1234567890")]
    public void EncryptDecrypt_ShouldWork_ForVariousStringLengths(string plainText)
    {
        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        var decrypted = _encryptionService.Decrypt(encrypted);
        
        // Assert
        decrypted.Should().Be(plainText);
    }
    
    [Fact]
    public void EncryptDecrypt_ShouldWork_ForSpecialCharacters()
    {
        // Arrange
        var plainText = "Tëst with spëcial chàràctérs: 日本語 العربية 中文 🎉";
        
        // Act
        var encrypted = _encryptionService.Encrypt(plainText);
        var decrypted = _encryptionService.Decrypt(encrypted);
        
        // Assert
        decrypted.Should().Be(plainText);
    }
}
