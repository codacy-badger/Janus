﻿using System.Collections.Generic;
using System.Linq;
using Janus.Matchers;

namespace Janus.Filters
{
    public class ExcludeFilter : IFilter
    {
        public FilterBehaviour Behaviour => FilterBehaviour.Ignore;

        private IPatternMatcher<string> _matcher = new SimpleStringMatcher();

        private IList<string> _filters;

        public IList<string> Filters => _filters;

        public ExcludeFilter(params string[] filters)
        {
            _filters = filters;
        }

        public ExcludeFilter(IList<string> filters)
        {
            _filters = filters;
        }

        public bool ShouldExcludeFile(string fullPath)
        {
            var ret = false;
            foreach (var filter in _filters)
            {
                if (_matcher.Matches(fullPath, filter))
                {
                    ret = true;
                }
            }
            return ret;
        }

        private bool Equals(ExcludeFilter other)
        {
            if (other.Behaviour != Behaviour || other.Filters.Count != Filters.Count) return false;
            return !Filters.Where((filter, i) => filter != other.Filters[i]).Any();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((ExcludeFilter)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_matcher?.GetHashCode() ?? 0) * 397) ^ (_filters?.GetHashCode() ?? 0);
            }
        }
    }
}
