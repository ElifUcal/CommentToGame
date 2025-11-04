using System.Globalization;
using System.Security.Claims;
using System.Text;
using CommentToGame.Data;
using CommentToGame.DTOs;
using CommentToGame.Models;
using CommentToGame.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CommentToGame.Controllers;

   [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IMongoCollection<Reviews> _reviews;
        private readonly IMongoCollection<ReviewVote> _reviewVotes;
        private readonly IMongoCollection<ReviewReply> _reviewReplies;
        private readonly IMongoCollection<ReplyVote> _replyVotes;

    public ReviewsController(MongoDbService db)
    {
        // Projene göre adı değiştir: "reviews"
        _reviews = db.GetCollection<Reviews>("reviews");
        _reviewVotes = db.GetCollection<ReviewVote>("review_votes");
        _reviewReplies = db.GetCollection<ReviewReply>("review_replies");
        _replyVotes = db.GetCollection<ReplyVote>("reply_votes");
    }
        
    private string GetUserId()
    {
        return User.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.NameIdentifier || c.Type == "id")?.Value
            ?? throw new UnauthorizedAccessException("Token geçersiz veya kullanıcı kimliği bulunamadı.");
    }
        [HttpGet]
        public async Task<ActionResult<PagedResult<Reviews>>> GetList(
            [FromQuery] string? gameId,
            [FromQuery] string? UserId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
    {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        var filter = Builders<Reviews>.Filter.Empty;
            
              if (!string.IsNullOrWhiteSpace(gameId))
            {
                if (!ObjectId.TryParse(gameId, out _))
                    return BadRequest("Invalid gameId.");
                filter &= Builders<Reviews>.Filter.Eq(x => x.GameId, gameId);
            }

            if (!string.IsNullOrWhiteSpace(UserId))
            {
                if (!ObjectId.TryParse(UserId, out _))
                    return BadRequest("Invalid userId.");
                filter &= Builders<Reviews>.Filter.Eq(x => x.UserId, UserId);
            }

            var total = await _reviews.CountDocumentsAsync(filter, cancellationToken: ct);

            var items = await _reviews
                .Find(filter)
                .Sort(Builders<Reviews>.Sort.Descending("_id"))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(ct);

            return Ok(new PagedResult<Reviews>
            {
                Page = page,
                PageSize = pageSize,
                Total = (int)total,
                Items = items
            });
        }


    [HttpGet("byGame/{gameId}")]
public async Task<ActionResult<PagedResult<ReviewViewDto>>> GetGameReviews(
    string gameId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? viewerUserId = null,
    CancellationToken ct = default)
{
    if (!ObjectId.TryParse(gameId, out var gameOid)) return BadRequest("Invalid gameId.");
    if (page <= 0) page = 1;
    if (pageSize <= 0 || pageSize > 200) pageSize = 20;

    var match = new BsonDocument("$match", new BsonDocument("GameId", gameOid));

    var lookupVotes = new BsonDocument("$lookup", new BsonDocument {
        { "from", "review_votes" },
        { "localField", "_id" },
        { "foreignField", "ReviewId" },
        { "as", "votes" }
    });

    var lookupReplies = new BsonDocument("$lookup", new BsonDocument {
        { "from", "review_replies" },
        { "localField", "_id" },
        { "foreignField", "ReviewId" },
        { "as", "replies" }
    });

    var addFields = new BsonDocument("$addFields", new BsonDocument {
        { "LikeCount", new BsonDocument("$size", new BsonArray {
            new BsonDocument("$filter", new BsonDocument {
                { "input", "$votes" },
                { "as", "v" },
                { "cond", new BsonDocument("$eq", new BsonArray { "$$v.Value", 1 }) }
            })
        })},
        { "DislikeCount", new BsonDocument("$size", new BsonArray {
            new BsonDocument("$filter", new BsonDocument {
                { "input", "$votes" },
                { "as", "v" },
                { "cond", new BsonDocument("$eq", new BsonArray { "$$v.Value", -1 }) }
            })
        })},
        { "ReplyCount", new BsonDocument("$size", "$replies") }
    });

    BsonDocument addMyVote =
        string.IsNullOrWhiteSpace(viewerUserId)
        ? new BsonDocument("$addFields", new BsonDocument("MyVote", 0))
        : (ObjectId.TryParse(viewerUserId, out var viewerOid)
            ? new BsonDocument("$addFields", new BsonDocument("MyVote",
                new BsonDocument("$let", new BsonDocument {
                    { "vars", new BsonDocument("mv",
                        new BsonDocument("$first",
                            new BsonDocument("$filter", new BsonDocument {
                                { "input", "$votes" },
                                { "as", "v" },
                                { "cond", new BsonDocument("$eq", new BsonArray { "$$v.UserId", viewerOid }) }
                            })
                        )
                    )},
                    { "in", new BsonDocument("$ifNull", new BsonArray { "$$mv.Value", 0 }) }
                })
            ))
            : throw new ArgumentException("Invalid viewerUserId."));

    var sort = new BsonDocument("$sort", new BsonDocument("_id", -1));

    // PROJECTION: hem PascalCase hem camelCase ihtimaline göre PascalCase üretelim
    var facet = new BsonDocument("$facet", new BsonDocument {
      { "items", new BsonArray {
        new BsonDocument("$skip", (page - 1) * pageSize),
        new BsonDocument("$limit", pageSize),
        new BsonDocument("$project", new BsonDocument {
          { "_id", 0 },
          { "Id",        new BsonDocument("$toString", "$_id") },        // <-- düzeltildi
          { "GameId",    new BsonDocument("$toString", "$GameId") },
          { "UserId",    new BsonDocument("$toString", "$UserId") },
          { "StarCount", "$StarCount" },
          { "Comment",   "$Comment" },
          { "IsSpoiler", "$isSpoiler" },
          { "TodayDate", "$TodayDate" },
          { "LikeCount", 1 },
          { "DislikeCount", 1 },
          { "MyVote", 1 },
          { "ReplyCount", 1 }
        })
      }},
      { "count", new BsonArray { new BsonDocument("$count", "total") } }
    });

    var pipeline = new[] { match, lookupVotes, lookupReplies, addFields, addMyVote, sort, facet };

    var result = await _reviews.Aggregate<BsonDocument>(pipeline, cancellationToken: ct)
                               .FirstOrDefaultAsync(ct);

    // ----- Manuel Map -----
    static string GetStr(BsonDocument d, string p, string c) =>
        d.TryGetValue(p, out var v1) ? (v1.IsString ? v1.AsString : v1.ToString()) :
        d.TryGetValue(c, out var v2) ? (v2.IsString ? v2.AsString : v2.ToString()) : string.Empty;

    static int GetInt(BsonDocument d, string p, string c) =>
        d.TryGetValue(p, out var v1) ? v1.ToInt32() :
        d.TryGetValue(c, out var v2) ? v2.ToInt32() : 0;

    static bool GetBool(BsonDocument d, string p, string c) =>
        d.TryGetValue(p, out var v1) ? v1.ToBoolean() :
        d.TryGetValue(c, out var v2) ? v2.ToBoolean() : false;

    static DateTime GetDate(BsonDocument d, string p, string c)
    {
        if (d.TryGetValue(p, out var v1)) return v1.ToUniversalTime();
        if (d.TryGetValue(c, out var v2)) return v2.ToUniversalTime();
        return DateTime.MinValue;
    }

    var docItems = result != null && result.Contains("items")
        ? result["items"].AsBsonArray.Select(x => x.AsBsonDocument).ToList()
        : new List<BsonDocument>();

    var items = docItems.Select(d => new ReviewViewDto {
        Id          = GetStr(d, "Id", "id"),
        GameId      = GetStr(d, "GameId", "gameId"),
        UserId      = GetStr(d, "UserId", "userId"),
        StarCount   = GetInt(d, "StarCount", "starCount"),
        Comment     = GetStr(d, "Comment", "comment"),
        IsSpoiler   = GetBool(d, "IsSpoiler", "isSpoiler"),
        TodayDate   = GetDate(d, "TodayDate", "todayDate"),
        LikeCount   = GetInt(d, "LikeCount", "likeCount"),
        DislikeCount= GetInt(d, "DislikeCount", "dislikeCount"),
        MyVote      = GetInt(d, "MyVote", "myVote"),
        ReplyCount  = GetInt(d, "ReplyCount", "replyCount"),
    }).ToList();

    var total = 0;
    if (result != null && result.Contains("count"))
    {
        var cntArr = result["count"].AsBsonArray;
        if (cntArr.Any())
        {
            var cntDoc = cntArr.First().AsBsonDocument;
            total = cntDoc.Contains("total") ? cntDoc["total"].ToInt32() : 0;
        }
    }

    return Ok(new PagedResult<ReviewViewDto> {
        Page = page,
        PageSize = pageSize,
        Total = total,
        Items = items
    });
}





    // GET /api/reviews/{id}
    [HttpGet("{id}")]
        public async Task<ActionResult<Reviews>> GetById(string id, CancellationToken ct = default)
        {
            if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid id.");

            var review = await _reviews.Find(x => x.Id == id).FirstOrDefaultAsync(ct);
            if (review == null) return NotFound();

            return Ok(review);
        }

        // POST /api/reviews
       [Authorize]
    [HttpPost]
    public async Task<ActionResult<Reviews>> Create([FromBody] ReviewCreateDto dto, CancellationToken ct = default)
    {
        var userId = GetUserId(); // 👈 JWT’den alıyoruz
        if (!ObjectId.TryParse(dto.GameId, out _)) return BadRequest("Invalid GameId.");
        if (!IsValidStar(dto.StarCount)) return BadRequest("StarCount must be between 1 and 5.");

        var trimmed = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment!.Trim();
        if (trimmed != null && trimmed.Length > 500)
            return BadRequest("Comment must be ≤ 500 characters.");

        var filter = Builders<Reviews>.Filter.Eq(x => x.GameId, dto.GameId) &
                     Builders<Reviews>.Filter.Eq(x => x.UserId, userId);

        var existing = await _reviews.Find(filter).FirstOrDefaultAsync(ct);

        var entity = new Reviews
        {
            Id = existing?.Id ?? ObjectId.GenerateNewId().ToString(),
            GameId = dto.GameId,
            UserId = userId, // 👈 Frontend yerine token’dan
            StarCount = dto.StarCount,
            Comment = trimmed,
            isSpoiler = dto.IsSpoiler
        };

        var options = new ReplaceOptions { IsUpsert = true };
        var result = await _reviews.ReplaceOneAsync(filter, entity, options, ct);

        if (existing == null && result.UpsertedId != null)
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);

        return Ok(entity);
    }


        // PUT /api/reviews/{id}
        // Not: PUT'ı "tam güncelleme" yerine pratik bir şekilde "kısmi" gibi kullandım.
        [HttpPut("{id}")]
        public async Task<ActionResult<Reviews>> Update(string id, [FromBody] ReviewUpdateDto dto, CancellationToken ct = default)
        {
            if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid id.");

            var updateDef = new List<UpdateDefinition<Reviews>>();
            var ub = Builders<Reviews>.Update;

            if (dto.StarCount.HasValue)
            {
                if (!IsValidStar(dto.StarCount.Value)) return BadRequest("StarCount must be between 1 and 5.");
                updateDef.Add(ub.Set(x => x.StarCount, dto.StarCount.Value));
            }

            if (dto.CommentSet)
            {
                var trimmed = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment!.Trim();
                updateDef.Add(ub.Set(x => x.Comment, trimmed));
            }

            if (dto.IsSpoiler.HasValue)
                updateDef.Add(ub.Set(x => x.isSpoiler, dto.IsSpoiler.Value));

            if (updateDef.Count == 0)
                return BadRequest("No fields to update.");

            var result = await _reviews.FindOneAndUpdateAsync(
                Builders<Reviews>.Filter.Eq(x => x.Id, id),
                ub.Combine(updateDef),
                new FindOneAndUpdateOptions<Reviews> { ReturnDocument = ReturnDocument.After },
                ct);

            if (result == null) return NotFound();
            return Ok(result);
        }

        // DELETE /api/reviews/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id, CancellationToken ct = default)
        {
            if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid id.");

            var delete = await _reviews.DeleteOneAsync(x => x.Id == id, ct);
            if (delete.DeletedCount == 0) return NotFound();

            return NoContent();
        }

    [HttpGet("stats/{gameId}")]
    public async Task<ActionResult<ReviewStatsDto>> GetStats(string gameId, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(gameId, out var gameOid)) return BadRequest("Invalid gameId.");

         var pipeline = new[]
    {
        new BsonDocument("$match", new BsonDocument("GameId", gameOid)),
        new BsonDocument("$group", new BsonDocument {
            { "_id", "$StarCount" },
            { "count", new BsonDocument("$sum", 1) }
        })
    };

        var buckets = await _reviews.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);

        var counts = new Dictionary<int, int>();
        int total = 0, sum = 0;
        foreach (var b in buckets)
        {
            var star = b["_id"].AsInt32;
            var cnt = b["count"].AsInt32;
            counts[star] = cnt; total += cnt; sum += star * cnt;
        }

        var dto = new ReviewStatsDto
        {
            GameId = gameId,
            Total = total,
            Average = total > 0 ? Math.Round((double)sum / total, 2) : 0.0,
            Distribution = Enumerable.Range(1, 5).ToDictionary(i => i, i => counts.TryGetValue(i, out var c) ? c : 0)
        };
        return Ok(dto);
    }


    // POST /api/reviews/{id}/vote  body: { userId, value = 1 | -1 }
[Authorize]
    [HttpPost("{id}/vote")]
    public async Task<IActionResult> VoteReview(string id, [FromBody] ReviewVoteDto dto, CancellationToken ct = default)
    {
        var userId = GetUserId(); // 👈 Token’dan al
        if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid review id.");
        if (dto.Value != 1 && dto.Value != -1) return BadRequest("Value must be 1 or -1.");

        var exists = await _reviews.Find(r => r.Id == id).Project(r => r.Id).FirstOrDefaultAsync(ct);
        if (exists == null) return NotFound("Review not found.");

        var filter = Builders<ReviewVote>.Filter.Eq(x => x.ReviewId, id) &
                     Builders<ReviewVote>.Filter.Eq(x => x.UserId, userId);

        var current = await _reviewVotes.Find(filter).FirstOrDefaultAsync(ct);

        if (current == null)
        {
            await _reviewVotes.InsertOneAsync(
            new ReviewVote { ReviewId = id, UserId = userId, Value = dto.Value },
            new InsertOneOptions(),
            ct);

            return Ok(new { myVote = dto.Value });
        }
        else if (current.Value == dto.Value)
        {
            await _reviewVotes.DeleteOneAsync(filter, ct);
            return Ok(new { myVote = 0 });
        }
        else
        {
            await _reviewVotes.UpdateOneAsync(filter, Builders<ReviewVote>.Update.Set(x => x.Value, dto.Value));
            return Ok(new { myVote = dto.Value });
        }
    }
    // DELETE /api/reviews/{id}/vote?userId=...
    [HttpDelete("{id}/vote")]
    public async Task<IActionResult> RemoveReviewVote(string id, [FromQuery] string userId, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid review id.");
        if (!ObjectId.TryParse(userId, out _)) return BadRequest("Invalid user id.");

        var filter = Builders<ReviewVote>.Filter.Eq(x => x.ReviewId, id) &
                     Builders<ReviewVote>.Filter.Eq(x => x.UserId, userId);

        await _reviewVotes.DeleteOneAsync(filter, ct);
        return NoContent();
    }

    // GET /api/reviews/{id}/replies?page=1&pageSize=20
    [HttpGet("{id}/replies")]
    public async Task<ActionResult<PagedResult<ReviewReplyDto>>> GetReplies(
        string id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid review id.");
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 200) pageSize = 20;

        var filter = Builders<ReviewReply>.Filter.Eq(x => x.ReviewId, id) &
                     Builders<ReviewReply>.Filter.Eq(x => x.DeletedAt, null);

        var total = await _reviewReplies.CountDocumentsAsync(filter, cancellationToken: ct);

        var items = await _reviewReplies.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var dto = items.Select(r => new ReviewReplyDto
        {
            Id = r.Id,
            ReviewId = r.ReviewId,
            UserId = r.UserId,
            Comment = r.Comment,
            IsSpoiler = r.IsSpoiler,
            CreatedAt = r.CreatedAt
        }).ToList();

        return Ok(new PagedResult<ReviewReplyDto>
        {
            Page = page,
            PageSize = pageSize,
            Total = (int)total,
            Items = dto
        });
    }


    // POST /api/reviews/{id}/replies
    [Authorize]
    [HttpPost("{id}/replies")]
    public async Task<ActionResult<ReviewReplyDto>> CreateReply(string id, [FromBody] ReviewReplyCreateDto dto, CancellationToken ct = default)
    {
        var userId = GetUserId(); // 👈 Token’dan al
        if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid review id.");
        if (string.IsNullOrWhiteSpace(dto.Comment)) return BadRequest("Comment required.");

        var exists = await _reviews.Find(r => r.Id == id).Project(r => r.Id).FirstOrDefaultAsync(ct);
        if (exists == null) return NotFound("Review not found.");

        var reply = new ReviewReply
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ReviewId = id,
            UserId = userId, // 👈 buraya yaz
            Comment = dto.Comment.Trim(),
            IsSpoiler = dto.IsSpoiler
        };

        await _reviewReplies.InsertOneAsync(reply, cancellationToken: ct);

        var outDto = new ReviewReplyDto
        {
            Id = reply.Id,
            ReviewId = reply.ReviewId,
            UserId = reply.UserId,
            Comment = reply.Comment,
            IsSpoiler = reply.IsSpoiler,
            CreatedAt = reply.CreatedAt
        };

        return CreatedAtAction(nameof(GetReplies), new { id, page = 1, pageSize = 1 }, outDto);
    }


    // DELETE /api/replies/{replyId}
    [HttpDelete("~/api/replies/{replyId}")]
    public async Task<IActionResult> DeleteReply(string replyId, CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(replyId, out _)) return BadRequest("Invalid reply id.");

        var update = Builders<ReviewReply>.Update.Set(x => x.DeletedAt, DateTime.UtcNow);
        var res = await _reviewReplies.UpdateOneAsync(x => x.Id == replyId && x.DeletedAt == null, update, cancellationToken: ct);
        if (res.MatchedCount == 0) return NotFound();

        return NoContent();
    }

    // POST /api/replies/{replyId}/vote  body: { userId, value }
    [Authorize]
    [HttpPost("~/api/replies/{replyId}/vote")]
    public async Task<IActionResult> VoteReply(string replyId, [FromBody] ReviewVoteDto dto, CancellationToken ct = default)
    {
        var userId = GetUserId(); // 👈 Token’dan al
        if (!ObjectId.TryParse(replyId, out _)) return BadRequest("Invalid reply id.");
        if (dto.Value != 1 && dto.Value != -1) return BadRequest("Value must be 1 or -1.");

        var exists = await _reviewReplies.Find(r => r.Id == replyId && r.DeletedAt == null)
                                         .Project(r => r.Id).FirstOrDefaultAsync(ct);
        if (exists == null) return NotFound("Reply not found.");

        var filter = Builders<ReplyVote>.Filter.Eq(x => x.ReplyId, replyId) &
                     Builders<ReplyVote>.Filter.Eq(x => x.UserId, userId);
        var current = await _replyVotes.Find(filter).FirstOrDefaultAsync(ct);

        if (current == null)
        {
            await _replyVotes.InsertOneAsync(new ReplyVote { ReplyId = replyId, UserId = userId, Value = dto.Value }, cancellationToken: ct);
            return Ok(new { myVote = dto.Value });
        }
        else if (current.Value == dto.Value)
        {
            await _replyVotes.DeleteOneAsync(filter, ct);
            return Ok(new { myVote = 0 });
        }
        else
        {
            await _replyVotes.UpdateOneAsync(filter, Builders<ReplyVote>.Update.Set(x => x.Value, dto.Value), cancellationToken: ct);
            return Ok(new { myVote = dto.Value });
        }
    }


    // GET /api/reviews/{id}/voters
[HttpGet("{id}/voters")]
public async Task<ActionResult<object>> GetReviewVoters(string id, CancellationToken ct = default)
{
    if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid review id.");

    // Review var mı? (opsiyonel ama iyi bir koruma)
    var exists = await _reviews.Find(r => r.Id == id).Project(r => r.Id).FirstOrDefaultAsync(ct);
    if (exists == null) return NotFound("Review not found.");

    var filter = Builders<ReviewVote>.Filter.Eq(x => x.ReviewId, id);

    // Sadece ihtiyacımız olan alanları çekelim
    var proj = Builders<ReviewVote>.Projection
        .Include(x => x.UserId)
        .Include(x => x.Value);

    var votes = await _reviewVotes
        .Find(filter)
        .Project<ReviewVote>(proj)
        .ToListAsync(ct);

    var likes = new List<object>();
    var dislikes = new List<object>();

    foreach (var v in votes)
    {
        // Ön uç beklediği için { userId } şeklinde objeler döndürüyoruz
        var row = new { userId = v.UserId };
        if (v.Value == 1) likes.Add(row);
        else if (v.Value == -1) dislikes.Add(row);
    }

    return Ok(new
    {
        reviewId = id,
        likeCount = likes.Count,
        dislikeCount = dislikes.Count,
        likes,
        dislikes
    });
}








    private static bool IsValidStar(int s) => s >= 1 && s <= 5;
    
    
}
