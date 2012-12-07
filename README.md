Temporal Versioning Bundle for RavenDB
======================================

This a custom bundle for RavenDB.  It requires RavenDB version 2.0.2170 or higher.

It allows you to make changes to a document that are effective at a particular point in time.  Often, this will be "now", but it can easily be a past or future date.  All version history is maintained.

The difference between this and Raven's official "Versioning Bundle" is that past changes are not hidden from you.  You can load, store, delete, or query at any date.  You can query on past data.  You can even build Temporal Map/Reduce Indexes that allow you to aggregate data at specific points in time.


### Manual Installation

Copy these files to the Plugins folder where your RavenDB Server is installed:

- Raven.Bundles.TemporalVersioning.dll
- Raven.Bundles.TemporalVersioning.Common.dll

Copy these files to your own solution and add a reference to them from your project(s).

- Raven.Client.Bundles.TemporalVersioning.dll
- Raven.Bundles.TemporalVersioning.Common.dll

Note that the *common* library goes in both places.

### NuGet Installation

Install the following packages where appropriate:

- [RavenDB.Bundles.TemporalVersioning](http://nuget.org/packages/RavenDB.Bundles.TemporalVersioning)
- [RavenDB.Client.TemporalVersioning](http://nuget.org/packages/RavenDB.Client.TemporalVersioning)

***TBD: These have not yet been published to NuGet.***

### Enabling the Bundle

Like all RavenDB bundles, the Temporal Versioning bundle must be enabled on each database that you plan on using it.

##### Enabling Temporal Versioning on a Standard Database

For normal RavenDB tenant databases, this is done by modifying the `Raven/Databases/YourDatabase` document in the Raven system database.  In the `Settings` property, the `Raven/ActiveBundles` setting controls which bundles are enabled for the database.  It is a comma-separated list of bundle names, and you can include `TemporalVersioning` in that list.  

To make this easier from code, you can call the following extension method after you create your database:

	documentStore.ActivateTemporalVersioningBundle(databaseName);

##### Enabling Temporal Versioning on an Embedded Database
If you are using an embedded RavenDB instance, you can enable the bundle with this code instead:

	documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(TemporalActivator).Assembly));

# Configuration

Enabling the bundle does not actually begin versioning documents until there is some configuration.  Temporal versioning can be configured for each document type, or globally for the entire database.

The bundle will first look for a document called `Raven/TemporalVersioning/Raven-Entity-Name` for each document type, where `Raven-Entity-Name` matches the corresponding metadata in the document.  If the document exists, and there is an `Enabled` property set to `true`, then temporal versioning is enabled for this document type.  If the document exists and `Enabled` is set to `false`, then temporal versioning is disabled for this document type.

If there is no configuration document found, or if there is no `Raven-Entity-Name` metadata, it will then look for a document called `Raven/TemporalVersioning/DefaultConfiguration` and use its `Enabled` property.  This allows you to enable temporal versioning globally for all documents, rather than for each document type.  It is recommended that you do not enable temporal versioning globally unless you understand the full impact.  It is usually better turning it on only when needed.

A few notes:

- Raven system documents (any document having an id starting with `Raven/`) are never versioned.
- Temporal Versioning should be enabled before storing any documents of a particular type.  Enabling temporal versioning on existing documents is not supported, and will probably delete your data.

To make this easier from code, you can use the following extension methods after you create your database:

    // per document type
    session.Advanced.ConfigureTemporalVersioning<YourEntity>(true);

    // globally
    session.Advanced.ConfigureTemporalVersioningDefaults(true);

# Usage

### A quick note on DateTimeOffset

Every date in the Temporal Versioning bundle is a `DateTimeOffset`.  This ensures that you are tracking *instantaneous time*, not *calendar time*.  This is an important concern, as it prevents you from having issues such as times in different time zones, and daylight savings time changes.

It doesn't matter what offset you provide, as things will be converted to UTC where needed.  But you should be aware of a few important quirks:

- You can pass a `DateTime` in to any `DateTimeOffset` field.  There is a one-way, implicit conversion.  But be aware of the `.Kind` property of your `DateTime` values.
- If you pass a `DateTime` with `Local` or `Unspecified` kind, the time zone of the computer where the code is running will be used and the `DateTime` value can change! `Local` comes from `DateTime.Now()` while `Unspecified` comes from `new DateTime()` or `DateTime.Parse()` when you don't explicitly set the `kind` parameter.
- `Utc` kinds are safer.  These come from methods such as `DateTime.UtcNow()`.
- It is much easier just to pass a `DateTimeOffset` instance.  They are unambiguousrelavent 
- Be aware that two `DateTimeOffset` values are equal if their UTC converted times are equal.  For example, `2012-01-01T00:00:00+00:00` and `2012-01-01T02:00:00+02:00` refer to the same instantaneous moment, and are therefore equivalent.  You can use an offset that is contextually relevant for your own purposes without regard to conversion.  If you have no context, or just don't care, then use UTC.
- RavenDB stores all `DateTime` and `DateTimeOffset` value in ISO8601 format.  This is available in .Net via  the round trip string formatter, `.ToString("o")`.
- A `DateTimeOffset` in a Raven document or metadata will maintain its offset, but when used in an index map, it will be converted to a UTC `DateTime`.  This is important and desired behavior such that sorting and filtering still honors the equality behavior described earlier.

### Temporal Session Operations

There are two extension methods added to Raven's `IDocumentSession` interface that are used to access temporal versions of the common session operations.  (Currently only synchronous sessions are supported)

    // access data at a particular moment
    session.Effective(dto).Whatever()

    // access current data
    session.EffectiveNow().Whatever()

By providing an effective date, revisions of the data can be made in the past, present, or future.  Both methods have the same effect. `EffectiveNow()` is just a convenience method that wraps `Effective(DateTimeOffset.UtcNow)` to make common operations less cumbersome.

The methods available from here are mostly the same as you're already used to with raven synchronous session methods, such as `.Store()`, `.Delete()`, `.Load()`, `.Query()`, and there are some new ones, as you will see in the examples below.

**Important** - Whenever writing a new revision of the data (regardless of create, update or delete), the effective date represents the date the revision goes into effect.  It is assumed that the revision will last *forever* until you provide the next revision.  If you provide a date that steps on existing data, the old data will no longer be part of the valid revision history.  The old revisions are not deleted, but they are turned into *artifacts* which are no longer valid representations of the data at any point in time.  Artifacts are useful can be used for audit trails.

For those familiar with the concepts of *bi-temporal* data, this bundle is mostly focused on tracking effectivity through the *valid time* dimension, while the artifacts provide access to the *transaction time* dimension.

##### Document Revisions and the Temporal Activator

Whenever you write a new revision for `foos/1`, the data is really stored at `foos/1/temporalrevisions/1`.  The `foos/1` is a virtual reference that means "get me the current data".

The Temporal Versioning bundle includes a server-side *Activator*, which is a background task responsible for making sure that the current revision is always accessible by its direct document key, by waiting for the appropriate time to copy the revision document back to the root document.

For example, I may have revision 1 effective at 1:00 and revision 2 effective at 4:00.  It's now 3:00 so when I load `foos/1` I will get back the first revision.  If I wait until 4:00 passes and load `foos/1` again, I'll get the second revision.  If I examine the database, I'll actually find three copies of the data.  In addition to `foos/1`, there will be `foos/1/temporalrevisions/1` and `foos/1/temporalrevisions/2`.  `foos/1` is always a current copy.  

Perhaps I then add a revision 3 that deletes the document at 6:00.  There are now four total documents, but when 6:00 hits, the `foos/1` current document is deleted. I still have all three revision documents.

### Examples

##### Storing a new document

    // most of the time, we store current data
    session.EffectiveNow().Store(foo);
    session.SaveChanges();

    // sometimes, we might store past or future data
    session.Effective(dto).Store(foo);
    session.SaveChanges();

#### Loading a document

    // we can still use a non-temporal load to get at "current" data
    var foo = session.Load<Foo>("foos/1");

    // this would have the same effect, but it's extraneous
    var foo = session.EffectiveNow().Load<Foo>("foos/1")

    // we certainly might want to load as of some past or future date
    var foo = session.Effective(dto).Load<Foo>("foos/1")

#### Updating a document

Normally, you would load a document, modify it, and save changes.  This will throw an exception with temporal data, as we didn't tell it when the new revision is effective.  Therefore, we introduce a new method, `PrepareNewRevision()`.

    // make a change as of now
    var foo = session.Load<Foo>("foos/1");
    session.PrepareNewRevision(foo);
    foo.Bar = 123;
    session.SaveChanges();

    // make a change at some past or future date
    var foo = session.Effective(dto).Load<Foo>("foos/1");
    session.PrepareNewRevision(foo, dto);
    foo.Bar = 123;
    session.SaveChanges();

**Important** - Be careful with edits at past or future dates.  Notice how I specify the effective date both when loading and preparing the revision.  It will still work if you skip the date when loading, but you may be copying other *current* data to your new date, and that may not be what you intended.

#### Deleting a document

When you delete a temporal document, you aren't really deleting it.  Instead, you are creating a new revision with the `Raven-Document-Temporal-Deleted` metadata value set to true.  This translation happens inside the bundle, so you can still issue a normal delete from the api.

    // delete as of now
    var foo = session.Load<Foo>("foos/1");
    session.EffectiveNow().Delete(foo);
    session.SaveChanges();

    // delete at some past or future date
    var foo = session.Effective(dto).Load<Foo>("foos/1");
    session.Effective(dto).Delete(foo);
    session.SaveChanges();

#### Querying

There are many different ways to query temporal data, some are simple, and some are complex.  There are probably other possibilities other than those described in this documentation, but here are some basics to get you started:

**Important** - Unlike RavenDB's standard Versioning bundle, the Temporal Versioning bundle does not exclude revisions from being indexed.  By default *everything* is indexed, including current data, revisions, and artifacts.  In order to avoid duplicates, the results must be filtered.  If you specify an effective date on your session, this is done for you server-side and the statistics `SkippedResults` and `TotalResults` will be affected.  This is important when paging your query results.  Refer to the [RavenDB Documentation](http://ravendb.net/docs/client-api/querying/paging) on paging with skipped results.

##### Querying data dynamically

    // query as of now
    var results = session.EffectiveNow().Query<Foo>().Where(x=> x.Bar == 123);

    // query at some past or future date
    var results = session.Effective(dto).Query<Foo>().Where(x=> x.Bar == 123);

##### Querying with a static index

If you want to *only* query current data, you can simply filter in the index and query non-temporally.  It's helpful to name the index such that you can remember that it is filtered.

    public class Foos_CurrentByBar : AbstractIndexCreationTask<Foo>
    {
        public Foos_CurrentByBar()
        {
            Map = foos => from foo in foos
                          let status = MetadataFor(foo).Value<TemporalStatus>(TemporalConstants.RavenDocumentTemporalStatus)
                          where status == TemporalStatus.Current
                          select new {
                                         foo.Bar
                                     };
        }
    }

    // Returns only current results.  The bundle doesn't have to filter because the index contained only current data.
    var results = session.Query<Foo, Foos_CurrentByBar>().Where(x=> x.Bar == 123);

If you might be querying for current data sometimes, and non-current data at other times, then it makes more sense to index everything and query temporally.

    public class Foos_ByBar : AbstractIndexCreationTask<Foo>
    {
        public Foos_ByBar()
        {
            Map = foos => from foo in foos
                          select new {
                                         foo.Bar
                                     };
        }
    }

    // Returns current results, because the bundle filtered the results to those that are effective now.
    var results = session.EffectiveNow().Query<Foo, Foos_ByBar>().Where(x=> x.Bar == 123);

    // Returns past or future results, because the bundle filtered the results to those that matched the effective date we asked for.
    var results = session.Effective(dto).Query<Foo, Foos_ByBar>().Where(x=> x.Bar == 123);

##### Map/Reduce of current data

Just like above, if you only care about current data, map/reduce is easy.  Just filter to current data when mapping, and everything works as normal.

When current data changes, either by putting a new revision or by the Temporal Activator promoting a revision to current, the index will be updated and the new totals will be reflected.

    public class Foos_CurrentCount : AbstractIndexCreationTask<Foo, Foos_CurrentCount.Result>
    {
        public class Result
        {
            public int Count { get; set; }
        }

        public Foos_CurrentCount()
        {
            Map = foos => from foo in foos
                          let status = MetadataFor(foo).Value<TemporalStatus>(TemporalConstants.RavenDocumentTemporalStatus)
                          where status == TemporalStatus.Current
                          select new {
                                         Count = 1
                                     };

            Reduce = results => from result in results
                                group result by 0
                                into g
                                select new {
                                               Count = g.Sum(x => x.Count)
                                           };
        }
    }

    // get the current count of foos
    var result = session.Query<Foos_CurrentCount.Result, Foos_CurrentCount>().First();
    var count = result.Count;

##### Temporal Map/Reduce of non-current data

Temporal Map/Reduce is needed if you want to be able to get totals at an arbitrary point in time.  This is a difficult problem to solve, and requires an advanced pattern that is currently difficult to express in RavenDB.  Refer to the `Employees_TemporalCount` index in the unit tests for an example of how it can be done.  Also be sure to look at the way that this data must be queried in order to get valid results.

There may be ways to express indexes more easily if one can pre-determine specific intervals to query.  For example, you might build a `Foos_DailyCounts` index that has the counts *per day*.  Unfortunately, this would probably require use of `Enumerable.Range` in the index map, which is currently unsupported in Raven.  When issue [RavenDB-757](http://issues.hibernatingrhinos.com/issue/RavenDB-757) is resolved, the documentation and tests will be updated with an example.

## Temporal Metadata
TBD

## Accessing Artifacts
TBD

