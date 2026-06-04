// Adapted from Funplay MCP for Unity (MIT). See THIRD_PARTY_NOTICES.md.

namespace Neo.UnityMcp.Execution
{
    // Implement this in a snippet passed to execute_code to opt into the structured
    // execution path: automatic Undo registration, change tracking, structured logs.
    //
    // Template:
    //   using UnityEngine;
    //   using Neo.UnityMcp.Execution;
    //   public class CommandScript : INeoCommand
    //   {
    //       public void Execute(NeoExecutionContext ctx)
    //       {
    //           var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    //           ctx.RegisterObjectCreation(go);   // Undo + tracking
    //           ctx.Log("Created {0}", go.name);
    //       }
    //   }
    public interface INeoCommand
    {
        void Execute(NeoExecutionContext ctx);
    }
}
