namespace D3B.Extractor.Unity;

/// <summary>Reads one object of a registry of managed references, with a reader at its first field.</summary>
/// <param name="id">The object's identifier, which references to it name.</param>
/// <param name="className">The object's class, with its namespace.</param>
/// <param name="reader">A reader laid out by the class's type tree.</param>
/// <returns>Whether the visitor read the whole object; when it did not, the registry skips the object itself.</returns>
internal delegate bool ReferenceVisitor(long id, string className, ref ObjectReader reader);
