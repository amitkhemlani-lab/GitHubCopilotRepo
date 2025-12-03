using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DotNetSample.Core;

namespace DotNetSample.Infrastructure
{
    public class EfRepository<T> : IRepository<T> where T: class
    {
        private readonly AppDbContext _db;
        private readonly DbSet<T> _set;

        public EfRepository(AppDbContext db)
        {
            _db = db;
            _set = _db.Set<T>();
        }

        public async Task AddAsync(T entity)
        {
            await _set.AddAsync(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _set.FindAsync(id);
            if (entity == null) return;
            _set.Remove(entity);
            await _db.SaveChangesAsync();
        }

        public async Task<T> GetAsync(Guid id)
        {
            return await _set.FindAsync(id);
        }

        public async Task<IEnumerable<T>> ListAsync()
        {
            return await _set.ToListAsync();
        }

        public async Task UpdateAsync(T entity)
        {
            _set.Update(entity);
            await _db.SaveChangesAsync();
        }
    }
}
