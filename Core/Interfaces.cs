using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNetSample.Core
{
    public interface IRepository<T>
    {
        Task<T> GetAsync(Guid id);
        Task<IEnumerable<T>> ListAsync();
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(Guid id);
    }

    public interface IOrderProcessor
    {
        Task ProcessPendingOrdersAsync();
    }
}
