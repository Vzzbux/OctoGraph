// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

var i = 0;
var interval;

function setLoadingText(text) {
    document.getElementById('loading').style.visibility = 'visible';
    document.getElementById('loading').textContent = text;
}

function stopLoading(close) {
    clearInterval(interval);
    if (close) {
        document.getElementById('loading').style.visibility = 'hidden';        
    }
    document.getElementById("refreshButton").disabled = false;
}

function loadOctopiDropdown() {
    let octopi = document.getElementById('envSelectList');
    let newDefault1 = new Option('-- Please Select --', null, true, true)
    newDefault1.disabled = true
    octopi.add(newDefault1)

    fetch('/api/octopi')
        .then(res => res.json())
        .then(data => {              
            for (var server in data) {
            let option = new Option(server, data[server])                
            octopi.add(option)
            }
        });
}

function loadGraph(evt) {
    document.getElementById("refreshButton").disabled = true;
    i = 0;
    interval = setInterval(function () {
        i = ++i % 4;
        setLoadingText("Loading " + Array(i + 1).join("."));
    }, 800);

    var forceCacheRefresh = false;
    if (typeof evt.currentTarget.forceCacheRefresh !== 'undefined') {
        forceCacheRefresh = evt.currentTarget.forceCacheRefresh;
    }    

    var env = document.getElementById("envSelectList").value;
    var linkSysAdminTeams = document.getElementById("linkSysAdminTeams").checked;
    var showUsers = document.getElementById("showUsers").checked;
    var showSpaces = document.getElementById("showSpaces").checked;        
    fetch('/api/graph?octopusServerLabel=' + encodeURI(env) 
        + '&forceCacheRefresh=' + forceCacheRefresh
        + '&linkSysAdminTeams=' + linkSysAdminTeams 
        + '&showUsers=' + showUsers
        + '&showSpaces=' + showSpaces,        
        { redirect: 'follow' })
        .then(res => {
            if (res.redirected) {
                console.log("Redirecting: " + res.url);
                window.location.href = res.url;
            }
            return res.json();
        })
        .then((out) => {
            initGraph(out);            
        }).catch(err => {
            stopLoading(false);            
            setLoadingText("Error: " + err);
            console.error(err);
        });
}

var networkGroups = {
            production: {
                shape: "icon",
                icon: {
                    face: "'Font Awesome 5 Free'",
                    weight: "900",
                    code: "\uf233",
                    size: 50,
                    color: 'crimson'
                }
            },
            preproduction: {
                shape: "icon",
                icon: {
                    face: "'Font Awesome 5 Free'",
                    weight: "900",
                    code: "\uf233",
                    size: 50,
                    color: 'orange'
                }
            },
            development: {
                shape: "icon",
                icon: {
                    face: "'Font Awesome 5 Free'",
                    weight: "900",
                    code: "\uf233",
                    size: 50,
                    color: 'lightblue'
                }
            },
            other: {
                shape: "icon",
                icon: {
                    face: "'Font Awesome 5 Free'",
                    weight: "900",
                    code: "\uf233",
                    size: 50,
                    color: 'lightgrey'
                }
            },
            users: {
                shape: "icon",
                icon: {
                    face: "'Font Awesome 5 Free'",
                    weight: "900", // Font Awesome 5 doesn't work properly unless bold.
                    code: "\uf007",
                    size: 50,
                    color: "#aa00ff"
                }
            },
            teams: {
                shape: "icon",
                icon: {
                    face: "'Font Awesome 5 Free'",
                    weight: "900", // Font Awesome 5 doesn't work properly unless bold.
                    code: "\uf0c0",
                    size: 50,
                    color: "#57169a"
                }
            }
       };

var mainnodes;
var serviceUrl;
function initGraph(dataWrapper) {
    serviceUrl = dataWrapper.serviceUrl;

    // create an array with nodes
    mainnodes = new vis.DataSet(dataWrapper.nodes);
    // create an array with edges
    var edges = new vis.DataSet(dataWrapper.edges);
    
    // create a network
    var container = document.getElementById('mynetwork');

    // provide the data in the vis format
    var data = {
        nodes: mainnodes,
        edges: edges
    };
    var options = {
        layout: { improvedLayout: true },
        interaction: { 
            keyboard: {
                enabled: true,
            },
            //hover: true 
        },        
        groups: networkGroups        
    };

    // initialize your network!
    if (document.fonts) {
        // Decent browsers: Make sure the fonts are loaded.
        document.fonts
            .load('normal normal 900 24px/1 "Font Awesome 5 Free"')
            .catch(
                console.error.bind(console, "Failed to load Font Awesome 5.")
            )
            .then(function () {
                // create a network
                var network = new vis.Network(container, data, options);
                network.once("stabilizationIterationsDone", function () {
                    stopLoading(true);
                });
                network.once("afterDrawing", () => {
                    container.style.height = '100vh';                    
                });
                network.on("doubleClick", doubleClickMain); 
            })
            .catch(
                console.error.bind(
                    console,
                    "Failed to render the network with Font Awesome 5."
                )
            );
    }
}

var keynodes;
function loadKey(container) {      
    keynodes =  new vis.DataSet([
        { id: 1, label: "Space", shape: "diamond", url: "https://octopus.com/docs/administration/spaces" },
        { id: 2, label: "Project Group", shape: "circle", url: "https://octopus.com/docs/getting-started/best-practices/project-and-project-groups" },
        { id: 3, label: "Project", shape: "ellipse", url: "https://octopus.com/docs/projects" },
        { id: 4, label: "Team", group: "teams", url: "https://octopus.com/docs/security/users-and-teams"},
        { id: 5, label: "User", group: "users",  url: "https://octopus.com/docs/security/users-and-teams"},
        { id: 6, label: "Production", group: "production"},
        { id: 7, label: "PreProduction", group: "preproduction"},
        { id: 8, label: "Development", group: "development"},
        { id: 9, label: "Other", group: "other"}]);

    var edges = new vis.DataSet({});
    for (let i = 1; i < keynodes.length; i++) { 
        if (i % 5 != 0) {
            edges.add({"from": i,"to": i+1,"color": "rgba(0, 0, 0, 0)"});
        }
    }

    var data = {
        nodes: keynodes,
        edges: edges,
    };
    var options = {
        layout: {
            hierarchical: {
              direction: "LR", // up-down
              //nodeSpacing: 0,
              levelSeparation: 100,
              treeSpacing: 100
            },
        },
        physics: { enabled: false },
        interaction: { dragNodes: false, dragView: false, zoomView: false, selectable: true, selectConnectedEdges: false, hoverConnectedEdges: false},
        groups: networkGroups        
    };    

    if (document.fonts) {
        // Decent browsers: Make sure the fonts are loaded.
        document.fonts
            .load('normal normal 900 24px/1 "Font Awesome 5 Free"')
            .catch(
                console.error.bind(console, "Failed to load Font Awesome 5.")
            )
            .then(function () {
                // create a network
                var keynetwork = new vis.Network(container, data, options);        
                keynetwork.once("stabilizationIterationsDone", function () {
                    stopLoading(true);
                });
                keynetwork.once("afterDrawing", () => {
                    container.style.height = '200px'
                });
                keynetwork.on("doubleClick", doubleClickKey);                
            })
            .catch(
                console.error.bind(
                    console,
                    "Failed to render the network with Font Awesome 5."
                )
            );
    }    
}

function doubleClickMain(evt) {
    doubleClick(evt, mainnodes, serviceUrl);
}

function doubleClickKey(evt) {
    doubleClick(evt, keynodes, "");
}

function doubleClick(evt, nodes, service) {    
    if (evt.nodes.length === 1) {
        var node = nodes.get(evt.nodes[0]);
        if (node.url != null) {
            window.open(service + node.url, '_blank');
        }
    }
}

