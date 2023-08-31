package main

import (
	"fmt"
	"os"
	"strconv"

	"github.com/eniac/Beldi/pkg/beldilib"
)

var services = []string{"elle", "data"}

func tables() {
	for _, service := range services {
		for {
			beldilib.CreateLambdaTables(service)
			if beldilib.WaitUntilAllActive([]string{service, fmt.Sprintf("%s-collector", service), fmt.Sprintf("%s-log", service)}) {
				break
			}
		}
	}
	ss := []string{"elle", "data"}
	for _, service := range ss {
		for {
			beldilib.CreateMainTable(fmt.Sprintf("%s-local", service))
			if beldilib.WaitUntilActive(fmt.Sprintf("%s-local", service)) {
				break
			}
		}
	}
	// beldilib.CreateLambdaTables("elle")
	// beldilib.WaitUntilAllActive([]string{"elle", "elle-collector", "elle-log"})

	// beldilib.CreateMainTable("elle-local")
	// beldilib.WaitUntilActive("elle-local")

	// beldilib.CreateLambdaTables("data")
	// beldilib.WaitUntilAllActive([]string{"data", "data-collector", "data-log"})

	// beldilib.CreateMainTable("data-local")
	// beldilib.WaitUntilActive("data-local")
}

func delete_tables() {
	for _, service := range services {
		beldilib.DeleteLambdaTables(service)
		beldilib.WaitUntilAllDeleted([]string{service, fmt.Sprintf("%s-collector", service), fmt.Sprintf("%s-log", service)})
	}
	ss := []string{"elle", "data"}
	for _, service := range ss {
		beldilib.DeleteTable(fmt.Sprintf("%s-local", service))
		beldilib.WaitUntilDeleted(fmt.Sprintf("%s-local", service))
	}

	// beldilib.DeleteLambdaTables("elle")
	// beldilib.WaitUntilAllDeleted([]string{"elle", "elle-collector", "elle-log"})

	// beldilib.DeleteTable("elle-local")
	// beldilib.WaitUntilDeleted("elle-local")

	// beldilib.DeleteLambdaTables("data")
	// beldilib.WaitUntilAllDeleted([]string{"data", "data-collector", "data-log"})

	// beldilib.DeleteTable("data-local")
	// beldilib.WaitUntilDeleted("data-local")
}

func populate(tables int) {
	for i := 0; i < tables; i++ {
		beldilib.Populate("data", strconv.Itoa(i), []int{}, false)
	}
	// beldilib.Populate("elle", "0", []int{}, false)
}

func main() {
	option := os.Args[1]
	if option == "create" {
		tables()
	} else if option == "clean" {
		delete_tables()
	} else if option == "populate" {
		tables, error := strconv.Atoi(os.Args[2])
		if error != nil {
			tables = 100
		}
		populate(tables)
	}
}
