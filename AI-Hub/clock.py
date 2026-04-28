# Install: pip install openai python-dotenv
import datetime
import json
import os

from dotenv import load_dotenv
from openai import OpenAI
import pytz

load_dotenv()

# Initialize the client
client = OpenAI(
    api_key=os.environ["OPENAI_API_KEY"],
    base_url=os.environ["OPENAI_BASE_URL"]
)

def get_time(zone: str) -> float:
    try:
        tz = pytz.timezone(zone)
        return datetime.datetime.now(tz)
    except pytz.exceptions.UnknownTimeZoneError:
        return None

available_functions = {
    "get_time": get_time,
}

tools = [
    {
        "type": "function",
        "name": "get_time",
        "description": "Gets the current local time in the given time zone.",
        "parameters": {
                "type": "object",
                "properties": {
                    "zone": {
                        "type": "string",
                        "description": "Time zone for which to get the current local time."
                    }
                },
            "additionalProperties": False,
            "required": ["zone"],
        }
    }
]

def execute_tool_call(tool_call) -> str | float:
    """
    Executes a tool call and returns the output.
    """
    fn_name = tool_call.name
    fn_args = json.loads(tool_call.arguments)

    if fn_name in available_functions:
        function_to_call = available_functions[fn_name]
        try:
            return function_to_call(**fn_args)
        except Exception as e:
            return f"Error calling {fn_name}: {e}"

    return f"Unknown tool: {fn_name}"

def chat_complete(messages):    
    response = client.responses.create(
        model="gpt-oss-120b-sovereign",
        input=messages,
        tools=tools,
        reasoning={"effort": "low"}
    )
    return response


def main():
    messages = [
        {"role": "developer", "content": "You are clock. Use the tool get_time to get the local time for a time zone."}
    ]

    while True:
        user_input = input("Your question (type 'exit' to end the conversation): ")
        if user_input == "exit":
            break

        messages.append({"role": "user", "content": user_input})
        response = chat_complete(messages)

        output = [x for x in response.output if x.type != "reasoning"][0]
        
        # add to chat history to keep track of the conversation
        messages.append(output)

        if output.type == "message":
            print(response.output_text)
            continue
        
        elif output.type == "function_call":
          tool_output = execute_tool_call(output)
          messages.append({
              "type": "function_call_output",
              "call_id": output.call_id,
              "output": str(tool_output),
          })

          response = chat_complete(messages)
          print(response.output_text)

        else:
          print(response.output_text)
    
    print(messages)


if __name__ == "__main__":
    main()