Temporal Versioning Bundle for RavenDB
======================================

This a custom bundle for RavenDB.  It requires RavenDB version 2.0.2170 or higher.

It allows you to make changes to a document that are effective at a particular point in time.  Often, this will be "now", but it can easily be a past or future date.  All version history is maintained.

The difference between this and Raven's official "Versioning Bundle" is that past changes are not hidden from you.  You can load, store, delete, or query at any date.  You can query on past data.  You can even build Temporal Map/Reduce Indexes that allow you to aggregate data at specific points in time.


### Manual Installation

Copy this file to the Plugins folder where your RavenDB Server is installed:

- Raven.Bundles.TemporalVersioning.dll

Copy this file to your own solution and add a reference to it from your project(s).

- Raven.Client.Bundles.TemporalVersioning.dll

### NuGet Installation

Install the following packages where appropriate:

- [RavenDB.Bundles.TemporalVersioning](http://nuget.org/packages/RavenDB.Bundles.TemporalVersioning)
- [RavenDB.Client.TemporalVersioning](http://nuget.org/packages/RavenDB.Client.TemporalVersioning)


### Client Initialization

You **must** initialize the Temporal Versioning client before using it.  This should occur once in your code, right after you initialize the document store.

    // Initialize the document store as usual.
    documentStore.Initialize();

    // Then initialize the temporal versioning client.
    documentStore.InitializeTemporalVersioning();

### Enabling the Server-Side Bundle

Like most RavenDB bundles, the Temporal Versioning bundle must be enabled on each database that you plan on using it.

##### Enabling Temporal Versioning on a Standard Database

For normal RavenDB tenant databases, this is done by modifying the `Raven/Databases/YourDatabase` document in the Raven system database.  In the `Settings` property, the `Raven/ActiveBundles` setting controls which bundles are enabled for the database.  It is a comma-separated list of bundle names, and you can include `TemporalVersioning` in that list.  

To make this easier from code, you can call the following extension method after you create your database:

	documentStore.ActivateTemporalVersioningBundle(databaseName);

##### Enabling Temporal Versioning on an Embedded Database
If you are using an embedded RavenDB instance, there is no need to put the bundle in a plugins folder.  Simply add both the bundle and the client to your project, and register the bundle with this code instead:

    documentStore.Configuration.RegisterTemporalVersioningBundle();

# Configuration

Enabling the bundle does not actually begin versioning documents until there is some configuration.  Temporal versioning can be configured for each document type, or globally for the entire database.

The bundle will first look for a document called `Raven/TemporalVersioning/Raven-Entity-Name` for each document type, where `Raven-Entity-Name` matches the corresponding metadata in the document.  If the document exists, and there is an `Enabled` property set to `true`, then temporal versioning is enabled for this document type.  If the document exists and `Enabled` is set to `false`, then temporal versioning is disabled for this document type.

If there is no configuration document found, or if there is no `Raven-Entity-Name` metadata, it will then look for a document called `Raven/TemporalVersioning/DefaultConfiguration` and use its `Enabled` property.  This allows you to enable temporal versioning globally for all documents, rather than for each document type.  It is recommended that you do not enable temporal versioning globally unless you understand the full impact.  It is usually better turning it on only when needed.

A few notes:

- Raven system documents (any document having an id starting with `Raven/`) are never versioned.
- **IMPORTANT** Temporal Versioning should be enabled before storing any documents of a particular type.  Enabling temporal versioning on existing documents is not currently supported, and will may delete your data.  (This will be supported in a future release.)

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
- It is much easier just to pass a `DateTimeOffset` instance.  They are unambiguous. 
- Be aware that two `DateTimeOffset` values are equal if their UTC converted times are equal.  For example, `2012-01-01T00:00:00+00:00` and `2012-01-01T02:00:00+02:00` refer to the same instantaneous moment, and are therefore equivalent.  You can use an offset that is contextually relevant for your own purposes without regard to conversion.  If you have no context, or just don't care, then use UTC.
- RavenDB stores all `DateTime` and `DateTimeOffset` value in ISO8601 format.  This is available in .Net via  the round trip string formatter, `.ToString("o")`.
- A `DateTimeOffset` in a Raven document or metadata will maintain its offset, but when used in an index map, it will be converted to a UTC `DateTime`.  This is important and desired behavior such that sorting and filtering still honors the equality behavior described earlier.

### Temporal Session Operations

Most of the time, you are interested in working with current data.  You do *not* need to specify anything special for this.  Just work with sessions as you normally would.

Sometimes you may want to work with past or future data.  For this, there is an extension method added to Raven's `IDocumentSession` interface that is used to access temporal versions of the common session operations.  (Currently only synchronous sessions are supported)

    // access data at a particular moment
    session.Effective(dto).Whatever()

The methods available from here are the same as you're already used to with raven synchronous session methods, such as `.Store()`, `.Delete()`, `.Load()`, and `.Query()`.

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
    session.Store(foo);
    session.SaveChanges();

    // sometimes, we might store past or future data
    session.Effective(dto).Store(foo);
    session.SaveChanges();

#### Loading a document

    // most of the time, we just load current data
    var foo = session.Load<Foo>("foos/1");

    // this would have the same effect, but it's extraneous
    // you should avoid using .Effective() for current data
    var foo = session.Effective(DateTimeOffset.UtcNow).Load<Foo>("foos/1")

    // we certainly might want to load as of some past or future date
    var foo = session.Effective(dto).Load<Foo>("foos/1")

#### Updating a document

We can make changes to a document at any effective date.  If you don't specify one, the change is effective immediately.

    // make a change as of now
    var foo = session.Load<Foo>("foos/1");
    foo.Bar = 123;
    session.SaveChanges();

    // make a change at some past or future date
    var foo = session.Effective(dto).Load<Foo>("foos/1");
    foo.Bar = 123;
    session.SaveChanges();

It's important to realize that any change made to the document will be made effective as of the same date it was loaded for.

#### Deleting a document

When you delete a temporal document, you aren't really deleting it.  Instead, you are creating a new revision with the `Raven-Document-Temporal-Deleted` metadata value set to true.  This translation happens inside the bundle, so you can still issue a normal delete from the api.

    // delete as of now
    var foo = session.Load<Foo>("foos/1");
    session.Delete(foo);
    session.SaveChanges();

    // delete at some past or future date
    var foo = session.Effective(dto).Load<Foo>("foos/1");
    session.Delete(foo);
    session.SaveChanges();

Just like with updates, the date you load the document for is the same effective date that will be applied to the delete.

#### Querying

There are many different ways to query temporal data, some are simple, and some are complex.  There are probably other possibilities other than those described in this documentation, but here are some basics to get you started:

**Important** - Unlike RavenDB's standard Versioning bundle, the Temporal Versioning bundle does not exclude revisions from being indexed.  By default *everything* is indexed, including current data, revisions, and artifacts.  In order to avoid duplicates, the results must be filtered.  If you specify an effective date on your session, this is done for you server-side and the statistics `SkippedResults` and `TotalResults` will be affected.  This is important when paging your query results.  Refer to the [RavenDB Documentation](http://ravendb.net/docs/client-api/querying/paging) on paging with skipped results.

##### Querying data dynamically

    // query current data
    var results = session.Query<Foo>().Where(x=> x.Bar == 123);

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
    var results = session.Query<Foo, Foos_ByBar>().Where(x=> x.Bar == 123);

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

## Getting an Audit Trail

Consider the following three events.

    // On January 1 we create a document.
    var dto = DateTimeOffset.Parse("2012-01-01T00:00:00Z")
    session.Effective(dto).Store(new Foo { Id = "foos/1", Bar = 123 });
    session.SaveChanges();

    // On March 1 we update the document with a new value.
    var dto = DateTimeOffset.Parse("2012-03-01T00:00:00Z")
    var foo = session.Effective(dto).Load("foos/1");
    foo.Bar = 456;
    foo.SaveChanges();

    // Sometime later, we decide that the data should have been different effective Feb 1.
    var dto = DateTimeOffset.Parse("2012-02-01T00:00:00Z")
    var foo = session.Effective(dto).Load("foos/1");
    foo.Bar = 999;
    foo.SaveChanges();

We have made three changes, but the third change was effective *before* the second one.  So the second change (with bar=456) is no longer valid data.  It is an *artifact*.  If we query the data, we will never receive this revision.

But what if we want to produce an audit trail? We need to see all historical changes for "foos/1".  This will include both revisions and artifacts.  We can do this without an index with the following code:

    // get all history for foos/1
    var revisions = session.Advanced.GetTemporalRevisionsFor("foos/1", start, pageSize);

You an also get back just the ids and then load them separately:
    
    var revisionIds = session.Advanced.GetTemporalRevisionIdsFor("foos/1", start, pageSize);

Both of these methods require pagination of their results.  For the transaction time, you can use the `Last-Modified` metadata value that Raven sets.

If you had more complex concerns for building your audit trail, you could use a static index such as the following:

    public class Foos_History : AbstractIndexCreationTask<Foo, Foos_History.Result>
    {
        public class Result
        {
            public string Id { get; set; }
            public DateTimeOffset TransactionTime { get; set; }
            public string ChangedBy { get; set; }
        }

        public Foos_History()
        {
            Map = foos => from foo in foos
                          let status = MetadataFor(foo).Value<TemporalStatus>(TemporalConstants.RavenDocumentTemporalStatus)
                          where status == TemporalStatus.Revision || status == TemporalStatus.Artifact
                          select new {
                                         foo.Id,
                                         TransactionTime = MetadataFor(foo)["Last-Modified"],
                                         ChangedBy = MetadataFor(foo)["Your-Custom-Metadata-For-Who-Made-The-Change"]
                                     };
        }
    }

    // just an example of what you might want to query
    var results = session.Query<Foos_History.Result, Foos_History>().Where(x=> x.Id.StartsWith("foos/1") && x.ChangedBy == "bob").OrderBy(x=> x.TransactionTime);

## Temporal Metadata

The Temporal Versioning Bundle adds several new metadata values to temporal documents.

- `Raven-Document-Temporal-Revision`  
The integer revision number, starting from 1.

- `Raven-Document-Temporal-Effective`  
A `DateTimeOffset` used transitively when loading or storing documents.
This is set by the client to inform the server of the intended effective date.
It is then returned on all documents so that the same date can be re-used for any changes.
It is never actually stored on the document, so you cannot query by it.

- `Raven-Document-Temporal-Effective-Start`  
The `DateTimeOffset` that the document becomes effective.

- `Raven-Document-Temporal-Effective-Until`  
The `DateTimeOffset` that the document is effective until.

**Note:** - The *Start* and *Until* dates form an inclusive/exclusive range over instantaneous valid time.
Using [interval notation](http://en.wikipedia.org/wiki/Interval_%28mathematics%29#Notations_for_intervals) -  `[start, until)`

- `Raven-Document-Temporal-Deleted`  
A `true` or `false` value indicating if this revision represents a deletion.

- `Raven-Document-Temporal-Pending`  
A `true` or `false` value indicating if this is a future revision that has not yet been made current.  It is pending activation.

- `Raven-Document-Temporal-Status`  
The temporal status of the document, one of the following values:

    * `Current` - The document is a root-level current document, for example foos/1.
    * `Revision` - The document is a revision, for example foos/1/temporalrevisions/1.
    * `Artifact` - The document is a revision that is no longer valid.
    * `NonTemporal` - Returned when trying to get temporal status from a non-temporal document.  `NonTemporal` is never actually stored on a document.

For convenience, you can access these via a few different ways.  These are all equivalent.

    // directly by string
    var metadata = session.Advanced.GetMetatadataFor(foo);
    string status = metadata.Value<string>("Raven-Temporal-Status");

    // the strings are all available as constants
    var metadata = session.Advanced.GetMetatadataFor(foo);
    string status = metadata.Value<string>(TemporalConstants.RavenDocumentTemporalStatus);

    // there is a strongly-typed access already wired up to these values
    var metadata = session.Advanced.GetMetatadataFor(foo);
    var temporal = metadata.GetTemporalMetadata();
    TemporalStatus status = temporal.Status;

    // to save you some key strokes, you can get it directly
    var temporal = session.Advanced.GetTemporalMetatadataFor(foo);
    TemporalStatus status = temporal.Status;

## Practical Application Guidance

Temporal Versioning is a great tool to have in your toolbox, but it is not a panacea.  It is best applied to entities where there is meaningful business value to tracking changes - beyond just an audit trail.  If all you need is an audit trail, use RavenDB's standard versioning bundle instead.

An easy use case to understand is for tracking changes in a payroll system.

    public class Employee
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public decimal PayRate { get; set; }
    }

The employee's name could change, such as often happens after a marriage.  The employee's pay rate could change, such as often happens with promotion or demotion.  `Employee` is a great candidate for Temporal Versioning, because we may need to reference the name and pay rate that were effective at a particular date - such as writing a new paycheck without invalidating a payrate from an old paycheck.  With Temporal Versioning applied, we do not have to create other entities in our domain to model this changing data.

Sometimes we may have properties in our temporal entities that we don't care about tracking, either because they can't change (such as `BirthDate`), or because we don't care about the changes (such as `FavoriteColor`). If there are just a few of these, then tracking them along with the other temporal data is just fine.  However, if you find there are many non-temporal properties and only a few temporal ones, then you may want to split these into separate documents.

We also don't want to forget another tried-and-true technique - point-in-time-duplication.  An easy use case for this is an online ordering system.

    public class Product
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }

    public class Order
    {
        public string Id { get; set; }
        public DateTimeOffset OrderDate { get; set; }
        public OrderLine[] Lines { get; set; }
        public decimal Total { get; set; }
    }

    public class OrderLine
    {
        public string ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

The *only* class here that should be temporal is the `Product`.  It could have temporal changes to its price, to reflect pricing changes, or to discontinue (delete) the product.

`Order` and `OrderLine` are **NOT** good candidates for Temporal Versioning.  Each `OrderLine` class gets a copy of the price that was in effect at the time the order was placed.  This price never changes.  Sure, we could look up the price from the `Product` temporally - but this would make even simple lookups much more complicated than they would need to be.

If one looks carefully at various other scenarios, a pattern emerges.  Point-in-time-duplication should be used when there is *some other* contextual time reference.  In this case - it's the `OrderDate`.  Temporal Versioning is a way to *add* a time context where none exists.  So if you're unsure where to use it, ask yourself if the Aggregate Entity already has its own concept of time or not.

## Support

This is a free community-contributed add-on to RavenDB.  For help, post questions on either *(but not both)* the [RavenDB Google Group](http://groups.google.com/group/ravendb) or to [Stack Overflow](http://www.stackoverflow.com) using the `RavenDB` tag.