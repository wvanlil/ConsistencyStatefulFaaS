(ns jepsen.beldi
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
            [clojure.data.json :as json]))


(defn r   [_ _] {:type :invoke, :f :read, :value nil})
(defn w   [_ _] {:type :invoke, :f :write, :value (rand-int 5)})
(defn cas [_ _] {:type :invoke, :f :cas, :value [(rand-int 5) (rand-int 5)]})

(defrecord Client [conn]
  client/Client
  (open! [this test node]
    this)

  (setup! [this test])

  (invoke! [_ test op]
    (case (:f op)
      :read (assoc op :type :ok, :value (:body (httpclient/get "http://localhost:3000/read")))
      :write (assoc op :type :ok, :value (:body (httpclient/get "http://localhost:3000/write")))
      ;; :txn (assoc op :type :ok, :value (try 
      ;;                                       (read-string (:body (:Output (json/read-str (:body (httpclient/post "https://gateway.lambda-url.us-east-1.on.aws/" {:form-params {:InstanceId (str (rand-int 2147483647))
      ;;                                                                                                                                                                   :CallerName ""
      ;;                                                                                                                                                                   :Async true,
      ;;                                                                                                                                                                   :Input (str (:value op))}
      ;;                                                                                       :content-type :json
      ;;                                                                                       }))
                                                                                            
      ;;                                                                       :key-fn keyword))))
      ;;                                       (catch Exception e (throw (Exception. "MyErrorMessage"))) ;; Normal exception message receives error, but custom one loses information :(
                                                                                            ;; )
      :txn (assoc op :type :ok, :value
                                            (read-string (:Output (json/read-str (:body (httpclient/post "https://gatewayname.lambda-url.us-east-1.on.aws/" {:form-params {:InstanceId (str (rand-int 2147483647))
                                                                                                                                                                        :CallerName ""
                                                                                                                                                                        :Async true,
                                                                                                                                                                        :Input (str (:value op))}
                                                                                            :content-type :json
                                                                                            }))
                                                                                            
                                                                            :key-fn keyword)))
                                            ;;  Normal exception message receives error, but custom one loses information :(
                                                                                            
      )
      )
  )

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
          :client (Client. nil)
          ;; :checker (ela/check) ;; Could not get to work
          ;; :nemesis (nemesis/hammer-time "dotnet")
          :generator (->> (ela/gen {:key-count 10, :min-txn-length 1, :max-txn-length 2, :max-writes-per-key 4}) ;; max-writes-per-key
                          ;; (gen/mix [r w ela/gen])
                          (gen/stagger 1/10)
                          (gen/nemesis 
                            nil)  
                            ;; (cycle [(gen/sleep 10)
                            ;;   {:type :info :f :start}
                            ;;   (gen/sleep 10)
                            ;;   {:type :info :f :stop}]))
                          (gen/time-limit 60))
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
;;   (print (type (read-string (:Output (json/read-str (:body (httpclient/post "https://gateway.lambda-url.us-east-1.on.aws/" {:form-params {:InstanceId (str (rand-int 2147483647))
;;                                                                                                             :CallerName ""
;;                                                                                                             :Async true,
;;                                                                                                             :Input "[[:append 0 1] [:r 0 nil]]"}
;;                                                                                             :content-type :json
;;                                                                                             }))
;;           :key-fn keyword))))))
                                                                                            