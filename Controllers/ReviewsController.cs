using System.Globalization;
using System.Text;
using CommentToGame.Data;
using CommentToGame.DTOs;
using CommentToGame.Models;
using CommentToGame.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CommentToGame.Controllers;

   [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly IMongoCollection<Reviews> _reviews;

    public ReviewsController(MongoDbService db)
    {
        // Projene göre adı değiştir: "reviews"
        _reviews = db.GetCollection<Reviews>("reviews");
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
        public async Task<ActionResult<PagedResult<Reviews>>> GetGameReviews(
            string gameId,
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
        [HttpPost]
        public async Task<ActionResult<Reviews>> Create([FromBody] ReviewCreateDto dto, CancellationToken ct = default)
        {
            if (!ObjectId.TryParse(dto.GameId, out _)) return BadRequest("Invalid GameId.");
            if (!ObjectId.TryParse(dto.UserId, out _)) return BadRequest("Invalid UserId.");
            if (!IsValidStar(dto.StarCount)) return BadRequest("StarCount must be between 1 and 5.");

            var entity = new Reviews
            {
                Id = ObjectId.GenerateNewId().ToString(),
                GameId = dto.GameId,
                UserId = dto.UserId,
                StarCount = dto.StarCount,
                Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment!.Trim(),
                isSpoiler = dto.IsSpoiler
            };

            await _reviews.InsertOneAsync(entity, cancellationToken: ct);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
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
    if (!ObjectId.TryParse(gameId, out var oid)) return BadRequest("Invalid gameId.");

    var match = new BsonDocument("$match", new BsonDocument("GameId", oid));
    var group = new BsonDocument("$group", new BsonDocument
    {
        { "_id", "$StarCount" },
        { "count", new BsonDocument("$sum", 1) }
    });

    var pipeline = new[] { match, group };
    var buckets = await _reviews.Aggregate<BsonDocument>(pipeline, cancellationToken: ct).ToListAsync(ct);

    var counts = new Dictionary<int, int>();
    int total = 0, sum = 0;

    foreach (var b in buckets)
    {
        var star = b["_id"].AsInt32;
        var cnt  = b["count"].AsInt32;
        counts[star] = cnt;
        total += cnt;
        sum   += star * cnt;
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


        private static bool IsValidStar(int s) => s >= 1 && s <= 5;
    
    
}
