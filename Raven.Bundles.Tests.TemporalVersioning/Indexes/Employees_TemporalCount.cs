using System;
using System.Linq;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    public class Employees_TemporalCount : AbstractMultiMapIndexCreationTask<Employees_TemporalCount.Result>
    {
        public class Result
        {
            public DateTimeOffset Effective { get; set; }
            public int Count { get; set; }
        }

        public Employees_TemporalCount()
        {
            /*
             *   Temporal Map/Reduce is HARD.  It requires a "sum of deltas" pattern, which is slightly different than a regular summation.
             *   We will map the CHANGES to the data at each time point.  We can reduce those changes per distinct date, but that still gives us changes, not totals.
             *   To get totals, we have to transform the results to aggregate all changes from the begining of the set up to (and including) the date in question.
             *
             *   Simply summing all items will not work - it would always return zero, since all data comes into existence and eventually goes out of existence.
             *   Often this is at DateTimeOffset.MaxValue, but it does indeed terminate.
             *
             *   Note that for large data sets over long periods of time, querying could be a slow operation because the transform must go through every date up to the
             *   date in question.  It may be possible to introduce some sort of snapshotting to improve on this.  Raven doesn't have multi-reduce, so it may be difficult.
             */

            // Map a +1 on each Start date
            AddMap<Employee>(employees => from employee in employees
                                          let status = MetadataFor(employee).Value<TemporalStatus>(TemporalConstants.RavenDocumentTemporalStatus)
                                          let effective = MetadataFor(employee).Value<DateTimeOffset>(TemporalConstants.RavenDocumentTemporalEffectiveStart)
                                          let deleted = MetadataFor(employee).Value<bool>(TemporalConstants.RavenDocumentTemporalDeleted)
                                          where status == TemporalStatus.Revision && deleted == false
                                          select new {
                                                         Effective = effective,
                                                         Count = 1
                                                     });

            // Map a -1 on each Until date
            AddMap<Employee>(employees => from employee in employees
                                          let status = MetadataFor(employee).Value<TemporalStatus>(TemporalConstants.RavenDocumentTemporalStatus)
                                          let effective = MetadataFor(employee).Value<DateTimeOffset>(TemporalConstants.RavenDocumentTemporalEffectiveUntil)
                                          let deleted = MetadataFor(employee).Value<bool>(TemporalConstants.RavenDocumentTemporalDeleted)
                                          where status == TemporalStatus.Revision && deleted == false
                                          select new {
                                                         Effective = effective,
                                                         Count = -1
                                                     });

            // Reduce by date, consolidating the deltas for each date and throwing out the zeros.
            Reduce = results => from result in results
                                group result by result.Effective
                                into g
                                let count = g.Sum(x => x.Count)
                                where count != 0
                                select new {
                                               Effective = g.Key,
                                               Count = count
                                           };

            // Transform the count such that each date includes all of the counts before it.
            // The .ToList(), group/ungroup, and Convert.ToInt32() are hacks to get Raven to cooperate.
            TransformResults = (database, results) => from result in results.ToList()
                                                      group result by 0
                                                      into g
                                                      from z in g
                                                      select new {
                                                                     z.Effective,
                                                                     Count = g.Where(x => x.Effective <= z.Effective).Sum(x => Convert.ToInt32(x.Count))
                                                                 };

            // TODO: Both of these work, but which is faster?

            // TODO: Bigger problem: Even though we are summing things here, we still have to return all results when querying or dates get skipped and the toals are wrong.
            //       We might as well be doing all of it client-side.  Really need to find a way to get it all in the reduce and avoid a transform.

            //TransformResults = (database, results) => from result in results.ToList()
            //                                          group result by 0
            //                                          into g
            //                                          let list = g.Zip(g.Aggregate(new int[0], (a, x) => a.Concat(new[] { a.LastOrDefault() + Convert.ToInt32(x.Count) }).ToArray()), (x, i) => new { x.Effective, Count = i })
            //                                          from z in list
            //                                          select new
            //                                              {
            //                                                  z.Effective,
            //                                                  z.Count
            //                                              };
        }
    }
}
