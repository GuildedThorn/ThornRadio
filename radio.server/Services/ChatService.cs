using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Radio.Server.Models;

namespace Radio.Server.Services;

// Services/ChatService.cs
using MongoDB.Driver;

public class ChatService {
    
    private readonly IMongoCollection<ChatMessage> _messages;

    public ChatService(MongoDbService dbService) {
        _messages = dbService.GetChatMessageCollection();
        
        // Create TTL index for auto-deletion after 30 days
        var indexKeys = Builders<ChatMessage>.IndexKeys.Ascending(x => x.Timestamp);
        var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30) };
        _messages.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(indexKeys, indexOptions));
    }

    public async Task StoreMessageAsync(ChatMessage message) {
        await _messages.InsertOneAsync(message);
    }

    public async Task<List<ChatMessage>> GetRecentMessagesAsync(int limit = 100) {
        return await _messages.Find(_ => true)
            .SortByDescending(m => m.Timestamp)
            .Limit(limit)
            .ToListAsync();
    }
}