using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VaultFlow.Api.Data;
using VaultFlow.Api.Models;

namespace VaultFlow.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public TransactionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions()
    {
        return await _context.Transactions.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> PostTransaction(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTransactions), new { id = transaction.Id }, transaction);
    }
}