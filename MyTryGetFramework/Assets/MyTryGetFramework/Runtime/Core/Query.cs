using System;

namespace TryGet
{
    /// <summary>
    /// System 用来按 Aspect/Tag 组合筛选 Entity 的条件表达。
    /// 支持 All-of（全部包含）和 None-of（排除）两种谓词 (ADR-0009)。
    ///
    /// V0.3 起内部完全用 <see cref="BitArray256"/> 位运算实现 Matches，
    /// 替代 V0.1 的 HashSet&lt;Type&gt; 路径——10000 Entity × 10 Query 场景下 5x+ 性能提升。
    /// 公共 API（Builder.WithAll / WithNone / WithAllTag / WithNoneTag / Matches）不变。
    /// </summary>
    public sealed class Query
    {
        private readonly BitArray256 _allOfAspects;
        private readonly BitArray256 _noneOfAspects;
        private readonly BitArray256 _allOfTags;
        private readonly BitArray256 _noneOfTags;

        private Query(BitArray256 allOfAspects, BitArray256 noneOfAspects,
                      BitArray256 allOfTags, BitArray256 noneOfTags)
        {
            _allOfAspects = allOfAspects;
            _noneOfAspects = noneOfAspects;
            _allOfTags = allOfTags;
            _noneOfTags = noneOfTags;
        }

        /// <summary>
        /// 判断 Entity 是否匹配此 Query。完全用位运算，O(1)。
        /// </summary>
        public bool Matches(Entity entity)
        {
            if (entity == null || entity.IsDestroyed)
                return false;

            var aMask = entity.AspectMask;
            if (!aMask.ContainsAll(_allOfAspects)) return false;
            if (aMask.ContainsAny(_noneOfAspects)) return false;

            var tMask = entity.TagMask;
            if (!tMask.ContainsAll(_allOfTags)) return false;
            if (tMask.ContainsAny(_noneOfTags)) return false;

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
            private BitArray256 _allOfAspects;
            private BitArray256 _noneOfAspects;
            private BitArray256 _allOfTags;
            private BitArray256 _noneOfTags;

            internal Builder() { }

            /// <summary>
            /// Entity 必须拥有此 Aspect 类型。
            /// </summary>
            public Builder WithAll<T>() where T : Aspect
            {
                _allOfAspects.Add(TypeIndex<T>.Index);
                return this;
            }

            /// <summary>
            /// Entity 不得拥有此 Aspect 类型。
            /// </summary>
            public Builder WithNone<T>() where T : Aspect
            {
                _noneOfAspects.Add(TypeIndex<T>.Index);
                return this;
            }

            /// <summary>
            /// Entity 必须拥有此 Tag 类型。
            /// </summary>
            public Builder WithAllTag<T>() where T : Tag
            {
                _allOfTags.Add(TypeIndex<T>.Index);
                return this;
            }

            /// <summary>
            /// Entity 不得拥有此 Tag 类型。
            /// </summary>
            public Builder WithNoneTag<T>() where T : Tag
            {
                _noneOfTags.Add(TypeIndex<T>.Index);
                return this;
            }

            /// <summary>
            /// 构建不可变 Query 实例。
            /// </summary>
            public Query Build()
            {
                return new Query(_allOfAspects, _noneOfAspects, _allOfTags, _noneOfTags);
            }
        }
    }
}
