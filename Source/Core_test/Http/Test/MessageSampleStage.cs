using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using MAPE.Testing;


namespace MAPE.Http.Test {
	public enum MessageSampleStage {
		Initial = 0,
		Arranging,
		Acting,
		Asserting,
		Disposed
	}
}
