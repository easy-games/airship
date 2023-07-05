// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Text;
// using HandlebarsDotNet;
//
// namespace CsToTs {
//     
//     internal class SimpleEncoder : ITextEncoder {
//         static readonly Lazy<SimpleEncoder> _instance = new Lazy<SimpleEncoder>(() => new SimpleEncoder());
//
//         private SimpleEncoder() { }
//
//         public static SimpleEncoder Instance => _instance.Value;
//
//         string ITextEncoder.Encode(string value) => value;
//         public void Encode(StringBuilder text, TextWriter target) {
//             throw new NotImplementedException();
//         }
//
//         public void Encode(string text, TextWriter target) {
//             throw new NotImplementedException();
//         }
//
//         public void Encode<T>(T text, TextWriter target) where T : IEnumerator<char> {
//             throw new NotImplementedException();
//         }
//     }
// }