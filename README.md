# IndieGamesLab-Samples
A collection of messaging samples built using the [IndieGamesLab](https://github.com/spikesoftware/IndieGamesLab) framework.

## Technical Requirements
IGL.Samples is a Visual Studio solution with the following requirements:
* Visual Studio
* .Net 3.5, 4.5+
** The client is built in .Net 3.5 for compatibility with Unity3D
* Azure Subscription

## IndieGamesLab
Documentation is located at http://indiegameslab.com/.

# Sample Projects
## Echo Client
The Echo Client sends messages to the service bus and listens for response messages from the Echo Server task.  The sample illustrates basic connectivity and does not enforce handling the messages in any particular order.

## Echo Server reads messages from the service bus, determines the client that sent the message and sends the message back to the client.  The sample illustrates basic connectivity and does not enforce handling the messages in any particular order.

# Call to action!
What samples would you like created?  Join the community!  Create an issue or use Contact Us on the [http://indiegameslab.com/](IGL website).
