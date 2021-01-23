//using Comet.Internal.Reload;
using NUnit.Framework;
using System;
//using Comet.Samples;
using System.IO;
using System.Threading.Tasks;
//using Comet.Internal;

namespace Comet.Tests {
	[TestFixture ()]
	public class ReloadTests {
		static ReloadTests ()
		{
            HotReloadHelper.IsEnabled = true;
			var view = new Comet.View ();
		}
		IEvaluator eval;


		[Test()]
		public async Task CodeReplacesPRoperly()
		{

			var oldCode = File.ReadAllText ("SkiaSlider.txt");
			var newCode = Evaluator.ReplaceAllIndexesOf (oldCode, "SliderHandler", "SliderHandlerfdsjfdsklfjds");
			//Console.WriteLine (newCode);
			var expexted = File.ReadAllText ("SkiaSlider-result.txt");
			Assert.AreEqual (expexted, newCode);

		}

		[Test ()]
		public async Task CanLoadCodeFromFile ()
		{
			//Reset the evaluator;
			eval = new Evaluator ();
			var request = new EvalRequestMessage {
				Code = File.ReadAllText ("TestView.txt"),
				FileName = "BasicTestView.cs",
				Classes = new System.Collections.Generic.List<(string NameSpace, string ClassName)> {
					("Comet.Samples","BasicTestView")
				},
			};
			var result = new EvalResult ();
			var s = await eval.EvaluateCode (request, result);
			Assert.IsTrue (s);
		}

		[Test ()]
		public async Task CanReplaceAnExistingView ()
		{
			eval = new Evaluator ();
			var view = new BasicTestView ();
			Assert.IsNotNull (view);

			var request = new EvalRequestMessage {
				Code = File.ReadAllText ("TestView.txt"),
				FileName = "BasicTestView.cs",
				Classes = new System.Collections.Generic.List<(string NameSpace, string ClassName)> {
					("Comet.Samples","BasicTestView")
				},
			};
			var result = new EvalResult ();
			var s = await eval.EvaluateCode (request, result);
			foreach (var r in result.FoundClasses)
				HotReloadHelper.RegisterReplacedView (r.ClassName, r.Type);

			var newView = view.GetView ();
			var newViewtype = newView?.GetType ();
			Assert.AreSame (newViewtype, typeof (Text));
		}


		[Test ()]
		public async Task CanReloadTuples ()
		{
			eval = new Evaluator ();
			var request = new EvalRequestMessage {
				Code = File.ReadAllText ("TupleTestView.txt"),
				FileName = "MainPage.cs",
				Classes = new System.Collections.Generic.List<(string NameSpace, string ClassName)> {
					("Comet.Samples","MainPage")
				},
			};
			var result = new EvalResult ();
			var s = await eval.EvaluateCode (request, result);
			Assert.IsTrue (s);
		}
	}
}
