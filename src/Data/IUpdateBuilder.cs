using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using JsonApiDotNetCore.Models;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Data
{
    public interface IUpdateBuilder<T> where T : IIdentifiable
    {
        IUpdateBuilder<T> Set<TField>(Expression<Func<T, TField>> field, TField value);

        IUpdateBuilder<T> SetOnInsert<TField>(Expression<Func<T, TField>> field, TField value);

        IUpdateBuilder<T> Unset<TField>(Expression<Func<T, TField>> field);

        IUpdateBuilder<T> Inc(Expression<Func<T, int>> field, int value);

        IUpdateBuilder<T> RemoveAll<TItem>(Expression<Func<T, IEnumerable<TItem>>> field,
            Expression<Func<TItem, bool>> predicate);

        IUpdateBuilder<T> Add<TItem>(Expression<Func<T, IEnumerable<TItem>>> field, TItem value);
    }
}