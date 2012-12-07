Temporal Versioning Bundle for RavenDB
======================================

This a custom bundle for RavenDB.  It requires RavenDB version 2.0.2161 or higher.

It allows you to make changes to a document that are effective at a particular point in time.  Often, this will be "now", but it can easily be a past or future date.  All version history is maintained.

The difference between this and Raven's official "Versioning Bundle" is that past changes are not hidden from you.  You can load, store, delete, or query at any date.  You can query on past data.  You can even build Temporal Map/Reduce Indexes, that allow you to aggregate data at specific points in time.


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
- If you pass a `DateTime` with `Local` or `Unspecified` kind, the timezone of the computer where the code is running will be used and the `DateTime` value can change! `Local` comes from `DateTime.Now()` while `Unspecified` comes from `new DateTime()` or `DateTime.Parse()` when you don't explicitly set the `kind` parameter.
- `Utc` kinds are safer.  These come from methods such as `DateTime.UtcNow()`.
- It is much easier just to pass a `DateTimeOffset` instance.  They are unambiguous.
- Be aware that two `DateTimeOffset` values are equal if their UTC converted times are equal.  For example, `2012-01-01T00:00:00+00:00` and `2012-01-01T02:00:00+02:00` refer to the same instantaneous moment, and are therefore equivalent.  You can use an offset that is contextually relavent for your own purposes without regard to conversion.  If you have no context, or just don't care, then use UTC.
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

**Imporant** - Be careful with edits at past or future dates.  Notice how I specify the effective date both when loading and preparing the revision.  It will still work if you skip the date when loading, but you may be copying other *current* data to your new date, and that may not be what you intended.

#### Deleting a document

    // delete as of now
    var foo = session.Load<Foo>("foos/1");
    session.EffectiveNow().Delete(foo);

    // delete at some past or future date
    var foo = session.Load<Foo>("foos/1");
    session.Effective(dto).Delete(foo);

#### Querying

This is where things get fun.  There are many different ways to query temporal data, some are simple, some are complex.  Here are some basics to get you started:

##### Querying current data dynamically
TBD

##### Querying current data with a static index
TBD

##### Querying non-current data dynamically
TBD

##### Querying non-current data with a static index
TBD

##### Map/Reduce of current data
TBD

##### Temporal Map/Reduce of non-current data
TBD

## Temporal Metadata
TBD

## Accessing Artifacts
TBD

