using JsonApiDotNetCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SIL.Transcriber.Data
{
    public interface IRepository<T> where T : IIdentifiable
    {
        void Init();
        IQueryable<T> Query();

        Task InsertAsync(T entity);
        Task<bool> ReplaceAsync(T entity, bool upsert = false);
        Task<T> UpdateAsync(Expression<Func<T, bool>> filter, Action<IUpdateBuilder<T>> update, bool upsert = false);
        Task<T> DeleteAsync(Expression<Func<T, bool>> filter);
        Task<int> DeleteAllAsync(Expression<Func<T, bool>> filter);
    }
}
