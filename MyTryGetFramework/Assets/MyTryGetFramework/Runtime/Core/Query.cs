using System;
using System.Collections.Generic;

namespace TryGet
{
    /// <summary>
    /// System 用来按 Aspect/Tag 组合筛选 Entity 的条件表达。
    /// 支持 All-of（全部包含）和 None-of（排除）两种谓词 (ADR-0009)。
    /// </summary>
    public sealed class Query
    {
        private readonly HashSet<Type> _allOfAspects;
        private readonly HashSet<Type> _noneOfAspects;
        private readonly HashSet<Type> _allOfTags;
        private readonly HashSet<Type> _noneOfTags;

        private Query(HashSet<Type> allOfAspects, HashSet<Type> noneOfAspects,
                      HashSet<Type> allOfTags, HashSet<Type> noneOfTags)
        {
            _allOfAspects = allOfAspects;
            _noneOfAspects = noneOfAspects;
            _allOfTags = allOfTags;
            _noneOfTags = noneOfTags;
        }

        /// <summary>
        /// 判断 Entity 是否匹配此 Query。
        /// </summary>
        public bool Matches(Entity entity)
        {
            if (entity == null || entity.IsDestroyed)
                return false;

            // All-of Aspects：Entity 必须拥有全部指定 Aspect
            foreach (var type in _allOfAspects)
            {
                if (!entity.HasAspect(type))
                    return false;
            }

            // None-of Aspects：Entity 不得拥有任何指定 Aspect
            foreach (var type in _noneOfAspects)
            {
                if (entity.HasAspect(type))
                    return false;
            }

            // All-of Tags：Entity 必须拥有全部指定 Tag
            foreach (var type in _allOfTags)
            {
                if (!entity.HasTag(type))
                    return false;
            }

            // None-of Tags：Entity 不得拥有任何指定 Tag
            foreach (var type in _noneOfTags)
            {
                if (entity.HasTag(type))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 创建 Query 构建器。
        /// </summary>
        public static Builder Create()
        {
            return new Builder();
        }

        /// <summary>
        /// Query 构建器 — 流式 API。
        /// </summary>
        public sealed class Builder
        {
            private readonly HashSet<Type> _allOfAspects = new HashSet<Type>();
            private readonly HashSet<Type> _noneOfAspects = new HashSet<Type>();
            private readonly HashSet<Type> _allOfTags = new HashSet<Type>();
            private readonly HashSet<Type> _noneOfTags = new HashSet<Type>();

            internal Builder() { }

            /// <summary>
            /// Entity 必须拥有此 Aspect 类型。
            /// </summary>
            public Builder WithAll<T>() where T : Aspect
            {
                _allOfAspects.Add(typeof(T));
                return this;
            }

            /// <summary>
            /// Entity 不得拥有此 Aspect 类型。
            /// </summary>
            public Builder WithNone<T>() where T : Aspect
            {
                _noneOfAspects.Add(typeof(T));
                return this;
            }

            /// <summary>
            /// Entity 必须拥有此 Tag 类型。
            /// </summary>
            public Builder WithAllTag<T>() where T : Tag
            {
                _allOfTags.Add(typeof(T));
                return this;
            }

            /// <summary>
            /// Entity 不得拥有此 Tag 类型。
            /// </summary>
            public Builder WithNoneTag<T>() where T : Tag
            {
                _noneOfTags.Add(typeof(T));
                return this;
            }

            /// <summary>
            /// 构建不可变 Query 实例。
            /// </summary>
            public Query Build()
            {
                return new Query(
                    new HashSet<Type>(_allOfAspects),
                    new HashSet<Type>(_noneOfAspects),
                    new HashSet<Type>(_allOfTags),
                    new HashSet<Type>(_noneOfTags));
            }
        }
    }
}
