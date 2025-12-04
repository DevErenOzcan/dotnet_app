namespace webapp.Models;

public class User
{
    public int Id { get; set; }
    
    // İster: İsim Soyisim
    public string Name { get; set; } 
    
    public string Username { get; set; }
    
    public string Password { get; set; }
    
    public string? Email { get; set; }
    
    // İster: Profil Resmi (Resmin dosya yolu)
    public string? ProfilePicturePath { get; set; }
}