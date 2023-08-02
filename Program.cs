using MessageBroker.data;
using MessageBroker.models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=MessageBroker.db"));
var app = builder.Build();

app.UseHttpsRedirection();

// create topic
app.MapPost("api/topics", async (AppDbContext context, Topic topic) =>
{
    await context.Topics.AddAsync(topic);
    await context.SaveChangesAsync();
    return Results.Created($"api/topics/{topic.Id}", topic);
});

// get all topics
app.MapGet("api/topics", async (AppDbContext context) =>
{
    var topics = await context.Topics.ToListAsync();
    return Results.Ok(topics);
});

// create a message
app.MapPost("api/topics/{id}/messages", async (AppDbContext context, int id, Message message) =>
{

    bool IsTopicExist = await context.Topics.AnyAsync(top => top.Id == id);
    if (!IsTopicExist)
    {
        return Results.NotFound("Topic does not exist");
    }

    var subs = context.Subscriptions.Where(sub => sub.TopicId == id);
    if (subs.Count() == 0)
    {
        return Results.NotFound("No subscription found for this topic");
    }

    foreach (var sub in subs)
    {
        Message msg = new Message
        {
            TopicMessage = message.TopicMessage,
            SubscriptionId = sub.Id,
            ExpiresAfter = message.ExpiresAfter,
            MessageStatus = message.MessageStatus
        };
        await context.Messages.AddAsync(msg);
    }
    await context.SaveChangesAsync();
    return Results.Ok("Message has been published");

});


// create subscription
app.MapPost("api/topics/{id}/subscriptions", async (AppDbContext context, int id, Subscription sub) =>
{
    bool IsTopicExist = await context.Topics.AnyAsync(top => top.Id == id);
    if (!IsTopicExist)
    {
        return Results.NotFound("Topic does not exist");
    }
    sub.TopicId = id;
    await context.Subscriptions.AddAsync(sub);
    await context.SaveChangesAsync();
    return Results.Created($"api/topics/{id}/subscriptions/{sub.Id}", sub);
});

// Get subscription messages
app.MapGet("api/subscriptions/{id}/messages", async (AppDbContext context, int id) =>
{
    bool isSubExist = await context.Subscriptions.AnyAsync(s => s.Id == id);
    if (!isSubExist)
    {
        return Results.NotFound("No subscriptions found");
    }

    var messages = context.Messages.Where(msg => msg.SubscriptionId == id && msg.MessageStatus != "SENT");
    if (messages.Count() == 0)
    {
        return Results.NotFound("No new messages");
    }

    foreach (var msg in messages)
    {
        msg.MessageStatus = "REQUESTED";
    }

    await context.SaveChangesAsync();
    return Results.Ok(messages);

});

// ack messages for a subcriber
app.MapPost("api/subscriptions/{id}/messages", async (AppDbContext context, int id, int[] confirmationIds) =>
{
    bool isSubExist = await context.Subscriptions.AnyAsync(s => s.Id == id);
    if (!isSubExist)
    {
        return Results.NotFound("No subscriptions found");
    }

    if (confirmationIds.Length <= 0)
    {
        return Results.BadRequest();
    }

    int count = 0;
    foreach (var confId in confirmationIds)
    {
        var msg = await context.Messages.FirstOrDefaultAsync(m => m.Id == confId);
        if (msg != null)
        {
            msg.MessageStatus = "SENT";
            count++;
        }
    }
    await context.SaveChangesAsync();
    return Results.Ok($"Acknowledged {count}/{confirmationIds.Length} messages");
});

app.Run();

