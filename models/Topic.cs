using System.ComponentModel.DataAnnotations;

namespace MessageBroker.models;

public class Topic
{
    [Key]
    public int Id { get; set; }
    [Required]
    public string? Name { get; set; }
}