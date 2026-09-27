namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>İsteği oluşturan kurum personelinin raporda gösterilecek kimlik bilgileri.</summary>
public sealed record PersonnelInfo(string? RegistrationNumber, string? FirstName, string? LastName);
