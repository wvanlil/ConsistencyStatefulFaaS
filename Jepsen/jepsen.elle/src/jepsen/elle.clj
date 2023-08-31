(ns jepsen.elle
  (:require [clojure.tools.logging :refer :all]
            [clojure.string :as str]
            [jepsen [cli :as cli]
                    [client :as client]
                    [control :as c]
                    [db :as db]
                    [generator :as gen]
                    [nemesis :as nemesis]
                    [tests :as tests]]
            [jepsen.control.util :as cu]
            [jepsen.os.debian :as debian]
            [elle.list-append :as ela]
            [clj-http.client :as httpclient]
            [slingshot.slingshot :refer [try+]]))

(def url "https://www.dropbox.com/s/dropboxlink/silo_v0.1.tar.gz?dl=0")

(def dir "/orleans")
(def binary "etcd")
(def logfile (str dir "/orleans.log"))
(def pidfile (str dir "/orleans.pid"))

(defn r   [_ _] {:type :invoke, :f :read, :value nil})
(defn w   [_ _] {:type :invoke, :f :write, :value (rand-int 5)})
(defn cas [_ _] {:type :invoke, :f :cas, :value [(rand-int 5) (rand-int 5)]})

(defn db
  "Etcd DB for a particular version."
  [version]
  (reify db/DB
    (setup! [_ test node]
      (info node "downloading and unpacking" version)

      (c/exec :apt-get :install :wget :-y)
      (c/exec :wget "https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb" :-O :packages-microsoft-prod.deb)
      (c/exec :dpkg :-i :packages-microsoft-prod.deb)
      (c/exec :rm :packages-microsoft-prod.deb)
      (c/exec :apt-get :update)
      (c/exec :apt-get :install :-y :apt-transport-https)
      (c/exec :apt-get :update)
      (c/exec :apt-get :install :-y :dotnet-runtime-6.0)

      (cu/install-archive! url dir)
      (cu/start-daemon!
      {
        :logfile logfile
        :pidfile pidfile
        :chdir dir
      }
      "/usr/bin/dotnet" "/orleans/Silo.dll")

      (Thread/sleep 15000)
      )

    (teardown! [_ test node]
      (info node "tearing down silo")
      (cu/stop-daemon! "dotnet" pidfile)
      (c/exec :rm :-rf dir)
      )

    ;; Logging asks for password, doesnt seem to work correctly...
    ;; db/LogFiles
    ;; (log-files [_ test node]
    ;;   [logfile])

    ))

(defrecord Client [conn]
  client/Client
  (open! [this test node]
    this)

  (setup! [this test])

  (invoke! [_ test op]
    (case (:f op)
      :txn (assoc op :type :ok, :value (try 
                                            (read-string (:body (httpclient/post "http://localhost:5000/" {:form-params {:operations (str (:value op))}
                                                                                            :content-type :json
                                                                                            })))
                                            (catch Exception e (throw (Exception. "MyErrorMessage"))) ;; Normal exception message receives error, but custom one loses information :(
                                                                                            )
      )
      
      
      )
  )

  ;;   (invoke! [_ test op]
  ;;   (case (:f op)
  ;;     :txn (assoc op :type :ok, :value (try 
  ;;                                           (read-string (:body (httpclient/post "http://localhost:5000/non-transactional" {:form-params {:value (rand-int 100)}
  ;;                                                                                           :content-type :json
  ;;                                                                                           })))
  ;;                                           (catch Exception e (throw (Exception. "MyErrorMessage"))) ;; Normal exception message receives error, but custom one loses information :(
  ;;                                                                                           )
  ;;     )
      
      
  ;;     )
  ;; )

  ;; (invoke! [_ test op]
  ;;   (try+
  ;;     (case (:f op)
  ;;       :txn (let [value (read-string (:body
  ;;         (httpclient/post "http://localhost:5000/" {:form-params {:operations (str (:value op))}
  ;;           :content-type :json
  ;;           })))]
  ;;           (assoc op :type :ok, :value value)

  ;;           )
  ;;       )

  ;;       (catch Exception e
  ;;         (assoc op :type :fail, :error :not-found))
        
  ;;     )
  ;; )

  (teardown! [this test])

  (close! [_ test]))

(defn etcd-test
  "Given an options map from the command line runner (e.g. :nodes, :ssh,
  :concurrency ...), constructs a test map."
  [opts]
  (merge tests/noop-test
         opts
         {:pure-generators true
          :name "etcd"
          :os   debian/os
          :db   (db "Orleans 0.1")
          :client (Client. nil)
          ;; :checker (ela/check) ;; Could not get to work
          ;; :nemesis (nemesis/partition-random-node)
          ;; :nemesis (nemesis/partition-random-halves)
          :nemesis (nemesis/compose {
            {
            :half-start :start
            :half-stop :stop} (nemesis/partition-random-halves)
            {
            :node-start :start
            :node-stop :stop} (nemesis/partition-random-node)
            {
            :hammer-start :start
            :hammer-stop :stop} (nemesis/hammer-time "dotnet")
          })
          ;; :nemesis (nemesis/hammer-time "dotnet")
          :generator (->> (ela/gen {:key-count 10, :min-txn-length 1, :max-txn-length 3, :max-writes-per-key 32}) ;; max-writes-per-key (32 default)
                          ;; (gen/mix [r w ela/gen])
                          (gen/stagger 1/10)
                          (gen/nemesis 
                            ;; nil)

                            (cycle [(gen/sleep 5)
                              {:type :info :f :half-start}
                              (gen/sleep 2)
                              {:type :info :f :half-stop}
                              (gen/sleep 5)
                              {:type :info :f :node-start}
                              (gen/sleep 2)
                              {:type :info :f :node-stop}
                              (gen/sleep 5)
                              {:type :info :f :hammer-start}
                              (gen/sleep 2)
                              {:type :info :f :hammer-stop}]
                              ))

                            ;; (cycle [(gen/sleep 5)
                            ;;   {:type :info :f :start}
                            ;;   (gen/sleep 2)
                            ;;   {:type :info :f :stop}]))
                          (gen/time-limit 100))
          }))

(defn -main
  "Handles command line arguments. Can either run a test, or a web server for
  browsing results."
  [& args]
  (cli/run! (merge (cli/single-test-cmd {:test-fn etcd-test})
                   (cli/serve-cmd))
            args))

;; (defn -main
;;   "Handles command line arguments. Can either run a test, or a web server for
;;   browsing results."
;;   [& args]
;;   (print (:body (httpclient/get "http://localhost:3000/"))))