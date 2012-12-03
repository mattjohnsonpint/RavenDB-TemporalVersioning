Temporal Versioning Bundle for RavenDB
==========================

This a custom bundle for RavenDB.  It requires RavenDB version 2.0.2161 or higher.

Basically, it allows you to make changes to a document that are effective at a particular point in time.  Often, this will be "now", but it can easily be a past or future date.  All version history is maintained.

The difference between this and Raven's official "Versioning Bundle" is that past changes are not hidden from you.  You can load, store, delete, or query at any date.  You can query on past data.  You can even build Temporal Map/Reduce Indexes, that allow you to aggregate data at specific points in time.

Detailed documentation and NuGet package are coming soon.  For now, take a look at the unit tests for examples of usage.