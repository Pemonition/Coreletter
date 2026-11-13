namespace RadarPlural.Domain.Posts;

// Salvamento privado: só o próprio membro enxerga o que salvou.
public class SavedPost
{
    public Guid MemberId { get; set; }
    public Guid PostId { get; set; }
}
